namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using DeliveryConstraints;
    using Extensibility;
    using Newtonsoft.Json;
    using NServiceBus;
    using Performance.TimeToBeReceived;
    using Routing;
    using ServiceControl.EndpointPlugin.Messages.SagaState;
    using Settings;
    using Transport;
    using Unicast.Transport;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;

    class ServiceControlBackend
    {
        public ServiceControlBackend(IDispatchMessages messageSender, ReadOnlySettings settings, CriticalError criticalError)
        {
            this.settings = settings;
            this.messageSender = messageSender;

            serializer = new JsonSerializer
            {
                TypeNameHandling = TypeNameHandling.None,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
            };

            serviceControlBackendAddress = this.settings.Get<string>("ServiceControl.Queue");

            circuitBreaker =
                new RepeatedFailuresOverTimeCircuitBreaker("ServiceControlConnectivity", TimeSpan.FromMinutes(2),
                    ex =>
                        criticalError.Raise(
                            "You have ServiceControl plugins installed in your endpoint, however, this endpoint is repeatedly unable to contact the ServiceControl backend to report endpoint information.", ex));
        }

        async Task Send(object messageToSend, TimeSpan timeToBeReceived, TransportTransaction transportTransaction)
        {
            var body = Serialize(messageToSend);

            var headers = new Dictionary<string, string>
            {
                [Headers.EnclosedMessageTypes] = messageToSend.GetType().FullName,
                [Headers.ContentType] = ContentTypes.Json,
                [Headers.ReplyToAddress] = settings.LocalAddress(),
                [Headers.MessageIntent] = sendIntent
            };

            try
            {
                var outgoingMessage = new OutgoingMessage(Guid.NewGuid().ToString(), headers, body);
                var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(serviceControlBackendAddress), deliveryConstraints: new List<DeliveryConstraint> { new DiscardIfNotReceivedBefore(timeToBeReceived) });
                await messageSender.Dispatch(new TransportOperations(operation), transportTransaction, new ContextBag()).ConfigureAwait(false);
                circuitBreaker.Success();
            }
            catch (Exception ex)
            {
                await circuitBreaker.Failure(ex).ConfigureAwait(false);
            }
        }

        byte[] Serialize(object messageToSend)
        {
            byte[] body;
            using (var memStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memStream))
                {
                    serializer.Serialize(writer, messageToSend);
                    writer.Flush();
                }
                body = memStream.ToArray();
            }
            return body;
        }

        public Task Send(SagaUpdatedMessage messageToSend, TransportTransaction transportTransaction)
        {
            return Send(messageToSend, TimeSpan.MaxValue, transportTransaction);
        }

        public async Task VerifyIfServiceControlQueueExists()
        {
            try
            {
                // In order to verify if the queue exists, we are sending a control message to SC.
                // If we are unable to send a message because the queue doesn't exist, then we can fail fast.
                // We currently don't have a way to check if Queue exists in a transport agnostic way,
                // hence the send.
                var outgoingMessage = ControlMessageFactory.Create(MessageIntentEnum.Send);
                outgoingMessage.Headers[Headers.ReplyToAddress] = settings.LocalAddress();
                var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(serviceControlBackendAddress));
                await messageSender.Dispatch(new TransportOperations(operation), new TransportTransaction(), new ContextBag()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                const string errMsg = @"You have ServiceControl plugins installed in your endpoint, however, this endpoint is unable to contact the ServiceControl Backend to report endpoint information.
Please ensure that the Particular ServiceControl queue specified is correct.";

                throw new Exception(errMsg, ex);
            }
        }

        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        IDispatchMessages messageSender;

        string serviceControlBackendAddress;
        ReadOnlySettings settings;

        readonly string sendIntent = MessageIntentEnum.Send.ToString();
        JsonSerializer serializer;
    }
}