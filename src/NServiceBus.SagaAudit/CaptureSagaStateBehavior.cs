﻿namespace ServiceControl.Plugin.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using EndpointPlugin.Messages.SagaState;
    using NServiceBus;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Saga;
    using NServiceBus.Sagas;

    class CaptureSagaStateBehavior : IBehavior<IncomingContext>
    {
        Configure configure;
        SagaUpdatedMessage sagaAudit;
        ServiceControlBackend backend;

        public CaptureSagaStateBehavior(Configure configure, ServiceControlBackend backend)
        {
            this.configure = configure;
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

            sagaAudit = new SagaUpdatedMessage
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
            AuditSaga(saga, context);
        }

        void AuditSaga(Saga saga, IncomingContext context)
        {
            string messageId;

            if (!context.IncomingLogicalMessage.Headers.TryGetValue(Headers.MessageId, out messageId))
            {
                return;
            }

            var activeSagaInstance = context.Get<ActiveSagaInstance>();
            var sagaStateString = Serializer.Serialize(saga.Entity);
            var messageType = context.IncomingLogicalMessage.MessageType.FullName;
            var headers = context.IncomingLogicalMessage.Headers;

            sagaAudit.Initiator = BuildSagaChangeInitatorMessage(headers, messageId, messageType);
            sagaAudit.IsNew = activeSagaInstance.IsNew;
            sagaAudit.IsCompleted = saga.Completed;
            sagaAudit.Endpoint = configure.Settings.EndpointName();
            sagaAudit.SagaId = saga.Entity.Id;
            sagaAudit.SagaType = saga.GetType().FullName;
            sagaAudit.SagaState = sagaStateString;

            AssignSagaStateChangeCausedByMessage(context);
            backend.Send(sagaAudit);
        }

        public SagaChangeInitiator BuildSagaChangeInitatorMessage(Dictionary<string, string> headers, string messageId, string messageType )
        {

            string originatingMachine;
            headers.TryGetValue(Headers.OriginatingMachine, out originatingMachine);

            string originatingEndpoint;
            headers.TryGetValue(Headers.OriginatingEndpoint, out originatingEndpoint);

            string timeSent;
            var timeSentConveredToUtc = headers.TryGetValue(Headers.TimeSent, out timeSent) ?
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
                    TimeSent = timeSentConveredToUtc,
                    Intent = intent
                };
        }

        void AssignSagaStateChangeCausedByMessage(IncomingContext context)
        {
            string sagaStateChange;

            if (!context.PhysicalMessage.Headers.TryGetValue("ServiceControl.SagaStateChange", out sagaStateChange))
            {
                sagaStateChange = String.Empty;
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

            if (!String.IsNullOrEmpty(sagaStateChange))
            {
                sagaStateChange += ";";
            }
            sagaStateChange += String.Format("{0}:{1}", sagaAudit.SagaId, statechange);

            context.PhysicalMessage.Headers["ServiceControl.SagaStateChange"] = sagaStateChange;
        }

    }
}