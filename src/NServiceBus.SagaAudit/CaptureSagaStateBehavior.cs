namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using Pipeline;
    using Sagas;
    using ServiceControl.EndpointPlugin.Messages.SagaState;
    using SimpleJson;
    using Transport;

    class CaptureSagaStateBehavior : Behavior<IInvokeHandlerContext>
    {
        ServiceControlBackend backend;
        Func<object, Dictionary<string, string>> customSagaEntitySerialization;
        string endpointName;
        static SagaEntitySerializationStrategy sagaEntitySerializationStrategy = new SagaEntitySerializationStrategy();

        CaptureSagaStateBehavior(string endpointName, ServiceControlBackend backend, Func<object, Dictionary<string, string>> customSagaEntitySerialization)
        {
            this.endpointName = endpointName;
            this.backend = backend;
            this.customSagaEntitySerialization = customSagaEntitySerialization;
        }

        public class CaptureSagaStateRegistration : RegisterStep
        {
            public CaptureSagaStateRegistration(string endpointName, ServiceControlBackend backend, Func<object, Dictionary<string, string>> customSagaEntitySerialization)
                : base("CaptureSagaState", typeof(CaptureSagaStateBehavior), "Records saga state changes", b => new CaptureSagaStateBehavior(endpointName, backend, customSagaEntitySerialization))
            {
                InsertBefore("InvokeSaga");
            }
        }

        public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
        {
            var sagaAudit = new SagaUpdatedMessage();

            context.Extensions.Set(sagaAudit);

            await next().ConfigureAwait(false);

            ActiveSagaInstance activeSagaInstance;

            if (!context.Extensions.TryGet(out activeSagaInstance))
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
            string messageId;

            if (!context.MessageHeaders.TryGetValue(Headers.MessageId, out messageId))
            {
                return Task.FromResult(0);
            }

            var saga = activeSagaInstance.Instance;
            string sagaStateString;
            if (customSagaEntitySerialization != null)
            {
                sagaStateString = SimpleJson.SerializeObject(customSagaEntitySerialization(saga.Entity));
            }
            else
            {
                sagaStateString = SimpleJson.SerializeObject(saga.Entity, sagaEntitySerializationStrategy);
            }

            var messageType = context.MessageMetadata.MessageType.FullName;
            var headers = context.MessageHeaders;

            sagaAudit.StartTime = activeSagaInstance.Created;
            sagaAudit.FinishTime = activeSagaInstance.Modified;
            sagaAudit.Initiator = BuildSagaChangeInitiatorMessage(headers, messageId, messageType);
            sagaAudit.IsNew = activeSagaInstance.IsNew;
            sagaAudit.IsCompleted = saga.Completed;
            sagaAudit.Endpoint = endpointName;
            sagaAudit.SagaId = saga.Entity.Id;
            sagaAudit.SagaType = saga.GetType().FullName;
            sagaAudit.SagaState = sagaStateString;

            AssignSagaStateChangeCausedByMessage(context, activeSagaInstance, sagaAudit);

            var transportTransaction = context.Extensions.Get<TransportTransaction>();
            return backend.Send(sagaAudit, transportTransaction);
        }

        public static SagaChangeInitiator BuildSagaChangeInitiatorMessage(IReadOnlyDictionary<string, string> headers, string messageId, string messageType)
        {
            string originatingMachine;
            headers.TryGetValue(Headers.OriginatingMachine, out originatingMachine);

            string originatingEndpoint;
            headers.TryGetValue(Headers.OriginatingEndpoint, out originatingEndpoint);

            string timeSent;
            var timeSentConvertedToUtc = headers.TryGetValue(Headers.TimeSent, out timeSent) ?
                DateTimeExtensions.ToUtcDateTime(timeSent) :
                DateTime.MinValue;

            string messageIntent;
            var intent = headers.TryGetValue(Headers.MessageIntent, out messageIntent) ? messageIntent : "Send"; // Just in case the received message is from an early version that does not have intent, should be a rare occasion.

            string isTimeout;
            var isTimeoutMessage = headers.TryGetValue(Headers.IsSagaTimeoutMessage, out isTimeout) && isTimeout.ToLowerInvariant() == "true";

            return new SagaChangeInitiator
            {
                IsSagaTimeoutMessage = isTimeoutMessage,
                InitiatingMessageId = messageId,
                OriginatingMachine = originatingMachine,
                OriginatingEndpoint = originatingEndpoint,
                MessageType = messageType,
                TimeSent = timeSentConvertedToUtc,
                Intent = intent
            };
        }

        static void AssignSagaStateChangeCausedByMessage(IInvokeHandlerContext context, ActiveSagaInstance sagaInstance, SagaUpdatedMessage sagaAudit)
        {
            string sagaStateChange;

            if (!context.MessageHeaders.TryGetValue("ServiceControl.SagaStateChange", out sagaStateChange))
            {
                sagaStateChange = string.Empty;
            }

            var statechange = "Updated";
            if (sagaInstance.IsNew)
            {
                statechange = "New";
            }
            if (sagaInstance.Instance.Completed)
            {
                statechange = "Completed";
            }

            if (!string.IsNullOrEmpty(sagaStateChange))
            {
                sagaStateChange += ";";
            }
            sagaStateChange += $"{sagaAudit.SagaId}:{statechange}";

            context.Headers["ServiceControl.SagaStateChange"] = sagaStateChange;
        }
    }
}