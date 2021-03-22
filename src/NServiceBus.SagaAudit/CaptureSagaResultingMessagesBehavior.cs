namespace NServiceBus.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Transport;
    using Pipeline;
    using Routing;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    class CaptureSagaResultingMessagesBehavior : Behavior<IOutgoingLogicalMessageContext>
    {
        public override Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
        {
            AppendMessageToState(context);
            return next();
        }

        void AppendMessageToState(IOutgoingLogicalMessageContext context)
        {
            var logicalMessage = context.Message;
            if (logicalMessage == null)
            {
                //this can happen on control messages
                return;
            }

            if (!context.Extensions.TryGet(out SagaUpdatedMessage sagaUpdatedMessage))
            {
                return;
            }

            TimeSpan? deliveryDelay = null;
            DateTimeOffset? doNotDeliverBefore = null;
            if (context.Extensions.TryGet(out DispatchProperties properties))
            {
                deliveryDelay = properties.DelayDeliveryWith?.Delay;
                doNotDeliverBefore = properties.DoNotDeliverBefore?.At;
            }

            var sagaResultingMessage = new SagaChangeOutput
            {
                ResultingMessageId = context.MessageId,
                TimeSent = DateTime.UtcNow,
                MessageType = logicalMessage.MessageType.ToString(),
                DeliveryDelay = deliveryDelay,
                DeliveryAt = doNotDeliverBefore?.UtcDateTime,
                Destination = GetDestinationForUnicastMessages(context),
                Intent = context.Headers[Headers.MessageIntent]
            };
            sagaUpdatedMessage.ResultingMessages.Add(sagaResultingMessage);
        }

        static string GetDestinationForUnicastMessages(IOutgoingLogicalMessageContext context)
        {
            var sendAddressTags = context.RoutingStrategies.OfType<UnicastRoutingStrategy>()
                .Select(urs => urs.Apply(context.Headers)).Cast<UnicastAddressTag>().ToList();
            return sendAddressTags.Count != 1 ? null : sendAddressTags.First().Destination;
        }
    }
}