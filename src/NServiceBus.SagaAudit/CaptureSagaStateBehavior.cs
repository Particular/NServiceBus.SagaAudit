namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using Pipeline;
    using Sagas;
    using ServiceControl.EndpointPlugin.Messages.SagaState;
    using Transport;

    sealed class CaptureSagaStateBehavior : IBehavior<IInvokeHandlerContext, IInvokeHandlerContext>
    {
        ServiceControlBackend backend;
        Func<object, Dictionary<string, string>>? customSagaEntitySerialization;
        JsonSerializerOptions? serializerOptions;
        string endpointName;

        public CaptureSagaStateBehavior(string endpointName, ServiceControlBackend backend, Func<object, Dictionary<string, string>>? customSagaEntitySerialization, JsonSerializerOptions? serializerOptions)
        {
            this.endpointName = endpointName;
            this.backend = backend;
            this.customSagaEntitySerialization = customSagaEntitySerialization;
            this.serializerOptions = serializerOptions;
        }

        public Task Invoke(IInvokeHandlerContext context, Func<IInvokeHandlerContext, Task> next)
        {
            if (context.MessageHandler.Instance is not Saga)
            {
                return next(context);
            }

            return InvokeSagaHandler(context, next);
        }

        async Task InvokeSagaHandler(IInvokeHandlerContext context, Func<IInvokeHandlerContext, Task> next)
        {
            var sagaAudit = new SagaUpdatedMessage();

            context.Extensions.Set(sagaAudit);

            await next(context).ConfigureAwait(false);

            if (!context.Extensions.TryGet(out ActiveSagaInstance? activeSagaInstance))
            {
                return; // Message was not handled by the saga
            }

            if (activeSagaInstance.Instance.Entity is null)
            {
                return; // Message was not handled by the saga
            }

            if (context.Extensions.TryGet<SagaAuditState>(out var auditState))
            {
                auditState.AddInvokedSaga(activeSagaInstance.Instance.GetType().FullName, activeSagaInstance.Instance.Entity.Id);
            }

            await AuditSaga(activeSagaInstance, sagaAudit, context).ConfigureAwait(false);
        }

        Task AuditSaga(ActiveSagaInstance activeSagaInstance, SagaUpdatedMessage sagaAudit, IInvokeHandlerContext context)
        {
            if (!context.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                return Task.CompletedTask;
            }

            var saga = activeSagaInstance.Instance;

            string sagaStateString = SerializeSagaState(saga.Entity);

            var messageType = context.MessageMetadata.MessageType.FullName;
            var headers = context.MessageHeaders;

            sagaAudit.StartTime = activeSagaInstance.Created.UtcDateTime;
            sagaAudit.FinishTime = activeSagaInstance.Modified.UtcDateTime;
            sagaAudit.Initiator = BuildSagaChangeInitiatorMessage(headers, messageId, messageType);
            sagaAudit.IsNew = activeSagaInstance.IsNew;
            sagaAudit.IsCompleted = saga.Completed;
            sagaAudit.Endpoint = endpointName;
            sagaAudit.SagaId = saga.Entity.Id;
            sagaAudit.SagaType = saga.GetType().FullName;
            sagaAudit.SagaState = sagaStateString;

            AssignSagaStateChangeCausedByMessage(context, activeSagaInstance, sagaAudit);

            var transportTransaction = context.Extensions.Get<TransportTransaction>();
            return backend.Send(sagaAudit, transportTransaction, context.CancellationToken);
        }

        internal string SerializeSagaState(IContainSagaData sagaData)
        {
            if (customSagaEntitySerialization is not null)
            {
                return JsonSerializer.Serialize(customSagaEntitySerialization(sagaData), SagaAuditJsonContext.Default.DictionaryStringString);
            }

            var sagaDataType = sagaData.GetType();
            var typeInfo = ResolveTypeInfo(serializerOptions, sagaDataType);

            return typeInfo is not null
                ? JsonSerializer.Serialize(sagaData, typeInfo)
                : SerializeWithReflection(sagaData, sagaDataType, serializerOptions);
        }

        static JsonTypeInfo? ResolveTypeInfo(JsonSerializerOptions? options, Type sagaDataType)
        {
            var typeInfo = options?.TypeInfoResolver?.GetTypeInfo(sagaDataType, options);
            if (typeInfo is not null)
            {
                return typeInfo;
            }

            return JsonSerializer.IsReflectionEnabledByDefault
                ? null
                : throw new InvalidOperationException($"No JSON metadata was found for the saga data type '{sagaDataType.FullName}'. Pass JsonSerializerOptions whose TypeInfoResolver includes it to AuditSagaStateChanges.");
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
        static string SerializeWithReflection(object sagaData, Type sagaDataType, JsonSerializerOptions? options) => JsonSerializer.Serialize(sagaData, sagaDataType, options);

        internal static SagaChangeInitiator BuildSagaChangeInitiatorMessage(IReadOnlyDictionary<string, string> headers, string messageId, string? messageType)
        {
            headers.TryGetValue(Headers.OriginatingMachine, out var originatingMachine);

            headers.TryGetValue(Headers.OriginatingEndpoint, out var originatingEndpoint);

            var timeSent = headers.TryGetValue(Headers.TimeSent, out var timeSentHeaderValue) ?
                DateTimeOffsetHelper.ToDateTimeOffset(timeSentHeaderValue) :
                DateTimeOffset.MinValue;

            var intent = headers.TryGetValue(Headers.MessageIntent, out var messageIntent) ? messageIntent : "Send"; // Just in case the received message is from an early version that does not have intent, should be a rare occasion.

            var isTimeoutMessage = headers.TryGetValue(Headers.IsSagaTimeoutMessage, out var isTimeout) && isTimeout.ToLowerInvariant() == "true";

            return new SagaChangeInitiator
            {
                IsSagaTimeoutMessage = isTimeoutMessage,
                InitiatingMessageId = messageId,
                OriginatingMachine = originatingMachine,
                OriginatingEndpoint = originatingEndpoint,
                MessageType = messageType,
                TimeSent = timeSent.UtcDateTime,
                Intent = intent
            };
        }

        static void AssignSagaStateChangeCausedByMessage(IInvokeHandlerContext context, ActiveSagaInstance sagaInstance, SagaUpdatedMessage sagaAudit)
        {
            var stateChange = "Updated";

            if (sagaInstance.IsNew)
            {
                stateChange = "New";
            }
            if (sagaInstance.Instance.Completed)
            {
                stateChange = "Completed";
            }

            if (context.Extensions.TryGet<SagaAuditState>(out var auditState))
            {
                auditState.AddSagaStateChange(sagaAudit.SagaId, stateChange);
            }
        }

        public class CaptureSagaStateRegistration : RegisterStep
        {
            public CaptureSagaStateRegistration(string endpointName, Func<object, Dictionary<string, string>>? customSagaEntitySerialization, JsonSerializerOptions? serializerOptions)
                : base("CaptureSagaState", typeof(CaptureSagaStateBehavior), "Records saga state changes", b => new CaptureSagaStateBehavior(endpointName, b.GetRequiredService<ServiceControlBackend>(), customSagaEntitySerialization, serializerOptions))
            {
                InsertBefore("InvokeSaga");
            }
        }
    }
}