namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Runtime.CompilerServices;
    using NUnit.Framework;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    [TestFixture]
    public class MessageSerialization
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SagaUpdated_serializes_correctly()
        {
            var entity = new SagaUpdatedMessage
            {
                SagaId = Guid.Empty,
                SagaState = "SagaState",
                Endpoint = "Endpoint",
                FinishTime = new DateTime(2017, 10, 30, 9, 22, 17, DateTimeKind.Utc),
                Initiator = new SagaChangeInitiator
                {
                    InitiatingMessageId = "InitiatingMessageId",
                    Intent = "intent",
                    IsSagaTimeoutMessage = true,
                    MessageType = "MessageType",
                    OriginatingEndpoint = "OriginatingEndpoint",
                    OriginatingMachine = "OriginatingMachine",
                    TimeSent = new DateTime(2017, 10, 30, 9, 22, 17, DateTimeKind.Utc)
                },
                IsCompleted = true,
                IsNew = true,
                ResultingMessages = { new SagaChangeOutput
                {
                    Destination = "Destination",
                    MessageType = "MessageType",
                    DeliveryAt = new DateTime(2017, 10, 30, 9, 22, 17, DateTimeKind.Utc),
                    DeliveryDelay = TimeSpan.FromSeconds(4000),
                    Intent = "Intent",
                    ResultingMessageId = "ResultingMessageId",
                    TimeSent = new DateTime(2017, 10, 30, 9, 22, 17, DateTimeKind.Utc)
                }},
                SagaType = "SagaType",
                StartTime = new DateTime(2017, 10, 30, 9, 22, 17, DateTimeKind.Utc)
            };
            var serialized = SimpleJson.SimpleJson.SerializeObject(entity, new MessageSerializationStrategy());
            TestApprover.Verify(serialized);
        }
        
    }
}