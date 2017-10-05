namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DeliveryConstraints;
    using Extensibility;
    using Logging;
    using NServiceBus;
    using Performance.TimeToBeReceived;
    using Routing;
    using ServiceControl.EndpointPlugin.Messages.SagaState;
    using Transport;
    using Unicast.Transport;

    class ServiceControlBackend
    {
        public ServiceControlBackend(string destinationQueue, string localAddress)
        {
            this.destinationQueue = destinationQueue;
            this.localAddress = localAddress;
        }

        async Task Send(object messageToSend, TimeSpan timeToBeReceived, TransportTransaction transportTransaction)
        {
            var body = Serialize(messageToSend);

            var headers = new Dictionary<string, string>
            {
                [Headers.EnclosedMessageTypes] = messageToSend.GetType().FullName,
                [Headers.ContentType] = ContentTypes.Json,
                [Headers.ReplyToAddress] = localAddress,
                [Headers.MessageIntent] = sendIntent
            };

            try
            {
                var outgoingMessage = new OutgoingMessage(Guid.NewGuid().ToString(), headers, body);
                var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(destinationQueue), deliveryConstraints: new List<DeliveryConstraint> { new DiscardIfNotReceivedBefore(timeToBeReceived) });
                await messageSender.Dispatch(new TransportOperations(operation), transportTransaction, new ContextBag()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn("Unable to send saga state change infromation to ServiceControl.", ex);
            }
        }

        static byte[] Serialize(object messageToSend)
        {
            return Encoding.UTF8.GetBytes(SimpleJson.SimpleJson.SerializeObject(messageToSend));
        }

        public Task Send(SagaUpdatedMessage messageToSend, TransportTransaction transportTransaction)
        {
            return Send(messageToSend, TimeSpan.MaxValue, transportTransaction);
        }

        public async Task Start(IDispatchMessages dispatcher)
        {
            messageSender = dispatcher;
            try
            {
                // In order to verify if the queue exists, we are sending a control message to SC.
                // If we are unable to send a message because the queue doesn't exist, then we can fail fast.
                // We currently don't have a way to check if Queue exists in a transport agnostic way,
                // hence the send.
                var outgoingMessage = ControlMessageFactory.Create(MessageIntentEnum.Send);
                outgoingMessage.Headers[Headers.ReplyToAddress] = localAddress;
                var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(destinationQueue));
                await messageSender.Dispatch(new TransportOperations(operation), new TransportTransaction(), new ContextBag()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                const string errMsg = @"You have ServiceControl plugins installed in your endpoint, however, this endpoint is unable to contact the ServiceControl Backend to report endpoint information.
Please ensure that the Particular ServiceControl queue specified is correct.";

                throw new Exception(errMsg, ex);
            }
        }

        IDispatchMessages messageSender;

        string destinationQueue;
        string localAddress;

        static ILog Logger = LogManager.GetLogger<ServiceControlBackend>();
        readonly string sendIntent = MessageIntentEnum.Send.ToString();
    }
}