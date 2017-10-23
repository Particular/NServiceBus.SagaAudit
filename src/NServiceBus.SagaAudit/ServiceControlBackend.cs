namespace NServiceBus.SagaAudit
{
    using System;
    using System.Text;
    using NServiceBus;
    using Logging;
    using Transports;
    using Unicast;
    using Unicast.Transport;
    using ServiceControl.EndpointPlugin.Messages.SagaState;
    using SimpleJson;

    class ServiceControlBackend
    {
        public Address Destination { get; set; }
        public Address LocalAddress { get; set; }

        public ServiceControlBackend(ISendMessages messageSender)
        {
            this.messageSender = messageSender;
        }

        public void Send(SagaUpdatedMessage messageToSend, TimeSpan timeToBeReceived)
        {
            var message = new TransportMessage
            {
                TimeToBeReceived = timeToBeReceived,
                Body = Serialize(messageToSend)
            };

            message.Headers[Headers.EnclosedMessageTypes] = messageToSend.GetType().FullName;
            message.Headers[Headers.ContentType] = ContentTypes.Json;

            try
            {
                messageSender.Send(message, new SendOptions(Destination) { ReplyToAddress = LocalAddress });
            }
            catch (Exception ex)
            {
                Logger.Warn("Unable to send saga state change infromation to ServiceControl.", ex);
            }
        }

        static byte[] Serialize(object messageToSend)
        {
            return Encoding.UTF8.GetBytes(SimpleJson.SerializeObject(messageToSend, serializerStrategy));
        }

        public void Send(SagaUpdatedMessage messageToSend)
        {
            Send(messageToSend, TimeSpan.MaxValue);
        }

        public void VerifyIfServiceControlQueueExists()
        {
            var sendOptions = new SendOptions(Destination) { ReplyToAddress = LocalAddress };
            try
            {
                // In order to verify if the queue exists, we are sending a control message to SC.
                // If we are unable to send a message because the queue doesn't exist, then we can fail fast.
                // We currently don't have a way to check if Queue exists in a transport agnostic way,
                // hence the send.
                messageSender.Send(ControlMessage.Create(), sendOptions);
            }
            catch (Exception ex)
            {
                const string errMsg = @"You have ServiceControl plugins installed in your endpoint, however, this endpoint is unable to contact the ServiceControl Backend to report endpoint information.
Please ensure that the Particular ServiceControl queue specified is correct.";

                throw new Exception(errMsg, ex);
            }
        }

        static IJsonSerializerStrategy serializerStrategy = new MessageSerializationStrategy();
        ISendMessages messageSender;
        static ILog Logger = LogManager.GetLogger<ServiceControlBackend>();
    }
}