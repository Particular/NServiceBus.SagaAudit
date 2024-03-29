﻿namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using NServiceBus;
    using Performance.TimeToBeReceived;
    using Routing;
    using ServiceControl.EndpointPlugin.Messages.SagaState;
    using Transport;
    using Unicast.Transport;

    class ServiceControlBackend
    {
        public ServiceControlBackend(string destinationQueue, ReceiveAddresses receiveAddresses)
        {
            this.destinationQueue = destinationQueue;
            localAddress = receiveAddresses.MainReceiveAddress;
        }

        async Task Send(object messageToSend, TimeSpan timeToBeReceived, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(messageToSend);

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

                var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(destinationQueue), new DispatchProperties { DiscardIfNotReceivedBefore = new DiscardIfNotReceivedBefore(timeToBeReceived) });
                await messageSender.Dispatch(new TransportOperations(operation), transportTransaction, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Logger.Warn("Unable to send saga state change information to ServiceControl.", ex);
            }
        }

        public Task Send(SagaUpdatedMessage messageToSend, TransportTransaction transportTransaction, CancellationToken cancellationToken = default)
        {
            return Send(messageToSend, TimeSpan.MaxValue, transportTransaction, cancellationToken);
        }

        public async Task Start(IMessageDispatcher dispatcher, CancellationToken cancellationToken = default)
        {
            messageSender = dispatcher;
            try
            {
                // In order to verify if the queue exists, we are sending a control message to SC.
                // If we are unable to send a message because the queue doesn't exist, then we can fail fast.
                // We currently don't have a way to check if Queue exists in a transport agnostic way,
                // hence the send.
                var outgoingMessage = ControlMessageFactory.Create(MessageIntent.Send);
                outgoingMessage.Headers[Headers.ReplyToAddress] = localAddress;
                var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(destinationQueue));
                await messageSender.Dispatch(new TransportOperations(operation), new TransportTransaction(), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                const string errMsg = @"You have enabled saga state change auditing in your endpoint, however, this endpoint is unable to contact the ServiceControl to report endpoint information.
Please ensure that the specified queue is correct.";

                throw new Exception(errMsg, ex);
            }
        }

        IMessageDispatcher messageSender;

        string destinationQueue;
        string localAddress;

        static ILog Logger = LogManager.GetLogger<ServiceControlBackend>();
        readonly string sendIntent = MessageIntent.Send.ToString();
    }
}