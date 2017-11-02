namespace NServiceBus.SagaAudit
{
    using System;
    using Pipeline;
    using Pipeline.Contexts;
    using ServiceControl.EndpointPlugin.Messages.SagaState;
    using Unicast;

    class CaptureSagaResultingMessagesBehavior : IBehavior<OutgoingContext>
    {
        SagaUpdatedMessage sagaUpdatedMessage;

        public void Invoke(OutgoingContext context, Action next)
        {
            AppendMessageToState(context);
            next();
        }

        void AppendMessageToState(OutgoingContext context)
        {
            if (!context.TryGet(out sagaUpdatedMessage))
            {
                return;
            }
            var logicalMessage = context.OutgoingLogicalMessage;
            if (logicalMessage == null)
            {
                //this can happen on control messages
                return;
            }

            var sagaResultingMessage = new SagaChangeOutput
            {
                ResultingMessageId = context.OutgoingMessage.Id,
                TimeSent = DateTimeExtensions.ToUtcDateTime(context.OutgoingMessage.Headers[Headers.TimeSent]),
                MessageType = logicalMessage.MessageType.ToString(),
                Intent = context.OutgoingMessage.Headers[Headers.MessageIntent]
            };

            if (context.DeliveryOptions is SendOptions sendOptions)
            {
                sagaResultingMessage.DeliveryDelay = sendOptions.DelayDeliveryWith;
                sagaResultingMessage.DeliveryAt = sendOptions.DeliverAt;
                sagaResultingMessage.Destination = sendOptions.Destination.ToString();
            }

            sagaUpdatedMessage.ResultingMessages.Add(sagaResultingMessage);
        }

        public class CaptureSagaResultingMessageRegistration : RegisterStep
        {
            public CaptureSagaResultingMessageRegistration()
                : base("ReportSagaStateChanges", typeof(CaptureSagaResultingMessagesBehavior), "Reports the saga state changes to ServiceControl")
            {
                InsertBefore(WellKnownStep.DispatchMessageToTransport);
            }
        }
    }
}