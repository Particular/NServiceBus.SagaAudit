namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using Pipeline;
    using Sagas;
    using ServiceControl.EndpointPlugin.Messages.SagaState;
    using Transport;

    class CaptureSagaStateBehavior : Behavior<IInvokeHandlerContext>
    {
        ServiceControlBackend backend;
        Func<object, Dictionary<string, string>> customSagaEntitySerialization;
        string endpointName;

        public CaptureSagaStateBehavior(string endpointName, ServiceControlBackend backend, Func<object, Dictionary<string, string>> customSagaEntitySerialization)
        {
            this.endpointName = endpointName;
            this.backend = backend;
            this.customSagaEntitySerialization = customSagaEntitySerialization;
        }

        public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
        {
            var sagaAudit = new SagaUpdatedMessage();

            context.Extensions.Set(sagaAudit);

            await next().ConfigureAwait(false);

            if (!context.Extensions.TryGet(out ActiveSagaInstance activeSagaInstance))
            {
                return; // Message was not handled by the saga
            }

            if (activeSagaInstance.Instance.Entity == null)
            {
                return; // Message was not handled by the saga
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
            if (customSagaEntitySerialization != null)
            {
                return JsonSerializer.Serialize(customSagaEntitySerialization(sagaData));
            }
            else
            {
                return JsonSerializer.Serialize(sagaData);
            }
        }

        internal static SagaChangeInitiator BuildSagaChangeInitiatorMessage(IReadOnlyDictionary<string, string> headers, string messageId, string messageType)
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
            if (!context.MessageHeaders.TryGetValue(SagaAuditHeaders.SagaStateChange, out var sagaStateChange))
            {
                sagaStateChange = string.Empty;
            }

            var stateChange = "Updated";

            if (sagaInstance.IsNew)
            {
                stateChange = "New";
            }
            if (sagaInstance.Instance.Completed)
            {
                stateChange = "Completed";
            }

            if (!string.IsNullOrEmpty(sagaStateChange))
            {
                sagaStateChange += ";";
            }
            sagaStateChange += $"{sagaAudit.SagaId}:{stateChange}";

            context.Headers[SagaAuditHeaders.SagaStateChange] = sagaStateChange;
        }

        public class CaptureSagaStateRegistration : RegisterStep
        {
            public CaptureSagaStateRegistration(string endpointName, Func<object, Dictionary<string, string>> customSagaEntitySerialization)
                : base("CaptureSagaState", typeof(CaptureSagaStateBehavior), "Records saga state changes", b => new CaptureSagaStateBehavior(endpointName, b.GetRequiredService<ServiceControlBackend>(), customSagaEntitySerialization))
            {
                InsertBefore("InvokeSaga");
            }
        }
    }
}