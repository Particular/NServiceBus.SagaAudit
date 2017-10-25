namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using SagaAudit;

    public class When_a_message_with_no_headers_arrive
    {
        [Test]
        public void Saga_state_change_message_can_be_created()
        {
            var headers = new Dictionary<string, string>();
            var messageId = Guid.NewGuid().ToString();
            var messageType = "SomeMessage";

            var message = CaptureSagaStateBehavior.BuildSagaChangeInitatorMessage(headers, messageId, messageType);

            Assert.IsNotNull(message);
            Assert.IsNull(message.OriginatingEndpoint);
            Assert.IsNull(message.OriginatingMachine);
            Assert.IsFalse(message.IsSagaTimeoutMessage);
            Assert.AreEqual(DateTime.MinValue, message.TimeSent); // When SC can handle null TimeSent, then should be asserting to null, instead of checking for minValue
        }
    }
}
