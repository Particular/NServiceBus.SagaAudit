namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using Pipeline;
    using Pipeline.Contexts;
    using Saga;
    using Sagas;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    class CaptureSagaStateBehavior : IBehavior<IncomingContext>
    {
        public string EndpointName { get; set; }
        public Func<object, Dictionary<string, string>> CustomSagaEntitySerialization { get; set; }

        ServiceControlBackend backend;
        static SagaEntitySerializationStrategy sagaEntitySerializationStrategy = new SagaEntitySerializationStrategy();

        public CaptureSagaStateBehavior(ServiceControlBackend backend)
        {
            this.backend = backend;
        }

        public void Invoke(IncomingContext context, Action next)
        {
            var saga = context.MessageHandler.Instance as Saga;

            if (saga == null)
            {
                next();
                return;
            }

            var sagaAudit = new SagaUpdatedMessage
            {
                StartTime = DateTime.UtcNow
            };
            context.Set(sagaAudit);
            next();

            if (saga.Entity == null)
            {
                return; // Message was not handled by the saga
            }

            sagaAudit.FinishTime = DateTime.UtcNow;
            AuditSaga(sagaAudit, saga, context);
        }

        void AuditSaga(SagaUpdatedMessage sagaAudit, Saga saga, IncomingContext context)
        {
            if (!context.IncomingLogicalMessage.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                return;
            }

            var activeSagaInstance = context.Get<ActiveSagaInstance>();

            string sagaStateString;
            if (CustomSagaEntitySerialization != null)
            {
                sagaStateString = SimpleJson.SimpleJson.SerializeObject(CustomSagaEntitySerialization(saga.Entity));
            }
            else
            {
                sagaStateString = SimpleJson.SimpleJson.SerializeObject(saga.Entity, sagaEntitySerializationStrategy);
            }

            var messageType = context.IncomingLogicalMessage.MessageType.FullName;
            var headers = context.IncomingLogicalMessage.Headers;

            sagaAudit.Initiator = BuildSagaChangeInitatorMessage(headers, messageId, messageType);
            sagaAudit.IsNew = activeSagaInstance.IsNew;
            sagaAudit.IsCompleted = saga.Completed;
            sagaAudit.Endpoint = EndpointName;
            sagaAudit.SagaId = saga.Entity.Id;
            sagaAudit.SagaType = saga.GetType().FullName;
            sagaAudit.SagaState = sagaStateString;

            AssignSagaStateChangeCausedByMessage(sagaAudit, context);
            backend.Send(sagaAudit);
        }

        public static SagaChangeInitiator BuildSagaChangeInitatorMessage(Dictionary<string, string> headers, string messageId, string messageType )
        {
            headers.TryGetValue(Headers.OriginatingMachine, out var originatingMachine);

            headers.TryGetValue(Headers.OriginatingEndpoint, out var originatingEndpoint);

            var timeSentConveredToUtc = headers.TryGetValue(Headers.TimeSent, out var timeSent) ?
                DateTimeExtensions.ToUtcDateTime(timeSent) :
                DateTime.MinValue;

            var intent = headers.TryGetValue(Headers.MessageIntent, out var messageIntent) ? messageIntent : "Send"; // Just in case the received message is from an early version that does not have intent, should be a rare occasion.

            var isTimeoutMessage = headers.TryGetValue(Headers.IsSagaTimeoutMessage, out var isTimeout) && isTimeout.ToLowerInvariant() == "true";

            return new SagaChangeInitiator
                {
                    IsSagaTimeoutMessage = isTimeoutMessage,
                    InitiatingMessageId = messageId,
                    OriginatingMachine = originatingMachine,
                    OriginatingEndpoint = originatingEndpoint,
                    MessageType = messageType,
                    TimeSent = timeSentConveredToUtc,
                    Intent = intent
                };
        }

        static void AssignSagaStateChangeCausedByMessage(SagaUpdatedMessage sagaAudit, IncomingContext context)
        {
            if (!context.PhysicalMessage.Headers.TryGetValue("ServiceControl.SagaStateChange", out var sagaStateChange))
            {
                sagaStateChange = string.Empty;
            }

            var statechange = "Updated";
            if (sagaAudit.IsNew)
            {
                statechange = "New";
            }
            if (sagaAudit.IsCompleted)
            {
                statechange = "Completed";
            }

            if (!string.IsNullOrEmpty(sagaStateChange))
            {
                sagaStateChange += ";";
            }
            sagaStateChange += $"{sagaAudit.SagaId}:{statechange}";

            context.PhysicalMessage.Headers["ServiceControl.SagaStateChange"] = sagaStateChange;
        }

        public class CaptureSagaStateRegistration : RegisterStep
        {
            public CaptureSagaStateRegistration()
                : base("CaptureSagaState", typeof(CaptureSagaStateBehavior), "Records saga state changes")
            {
                InsertBefore(WellKnownStep.InvokeSaga);
            }
        }
    }
}