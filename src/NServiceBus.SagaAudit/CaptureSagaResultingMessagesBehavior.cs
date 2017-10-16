namespace ServiceControl.Plugin.SagaAudit
{
    using System;
    using EndpointPlugin.Messages.SagaState;
    using NServiceBus;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Unicast;

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

            var sendOptions = context.DeliveryOptions as SendOptions;
            if (sendOptions != null)
            {
                var sagaResultingMessage = new SagaChangeOutput
                {
                    
                    ResultingMessageId = context.OutgoingMessage.Id,
                    TimeSent = DateTimeExtensions.ToUtcDateTime(context.OutgoingMessage.Headers[Headers.TimeSent]),
                    MessageType = logicalMessage.MessageType.ToString(),
                    DeliveryDelay = sendOptions.DelayDeliveryWith,
                    DeliveryAt = sendOptions.DeliverAt,
                    Destination = sendOptions.Destination.ToString(),
                    Intent = "Send" //TODO: How to get the proper message intent!?
                };
                sagaUpdatedMessage.ResultingMessages.Add(sagaResultingMessage);
            }

            if (context.DeliveryOptions is PublishOptions)
            {
                var sagaResultingMessage = new SagaChangeOutput
                {
                    ResultingMessageId = context.OutgoingMessage.Id,
                    TimeSent = DateTimeExtensions.ToUtcDateTime(context.OutgoingMessage.Headers[Headers.TimeSent]),
                    MessageType = logicalMessage.MessageType.ToString(),
                    //TODO: Can we remove the DeliveryDelay and DeliveryAt here??
                    //DeliveryDelay = publishOptions.DelayDeliveryWith,
                    //DeliveryAt = publishOptions.DeliverAt,
                    //Destination = GetDestination(context),
                    Intent = "Publish" //TODO: get the message intent the right way!
                };
                sagaUpdatedMessage.ResultingMessages.Add(sagaResultingMessage);
            }
        }
    }
}