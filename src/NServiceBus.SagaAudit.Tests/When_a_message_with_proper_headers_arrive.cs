namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using SagaAudit;

    public class When_a_message_with_proper_headers_arrive
    {
        [Test]
        public void Saga_state_change_message_can_be_created()
        {
            var headers = new Dictionary<string, string>
            {
                {"NServiceBus.MessageId", "cf79765e-0123-45bf-a41b-a42d00a867c9"},
                {"NServiceBus.CorrelationId", "cf79765e-0123-45bf-a41b-a42d00a867c9"},
                {"NServiceBus.MessageIntent", "Send"},
                {"NServiceBus.Version", "5.1.2"},
                {"NServiceBus.TimeSent", "2015-01-27 18:13:08:723699 Z"},
                {"NServiceBus.ContentType", "application/json"},
                {"NServiceBus.EnclosedMessageTypes", "Message1, NServiceBus.SagaAudit.Sample, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"},
                {"CorrId",@"cf79765e-0123-45bf-a41b-a42d00a867c9\0"},
                {"NServiceBus.ConversationId", "be34099f-1e50-4ec6-b935-a42d00a8681d"},
                {"WinIdName",@"MACHINE\user1"},
                {"NServiceBus.OriginatingMachine", "MACHINE"},
                {"NServiceBus.OriginatingEndpoint","NServiceBus.SagaAudit.Sample"},
                {"$.diagnostics.originating.hostid","8c436a19575e17e429c033fe4ab595fd"},
                {"NServiceBus.ReplyToAddress","NServiceBus.SagaAudit.Sample@MACHINE"},
                {"$.diagnostics.hostid","8c436a19575e17e429c033fe4ab595fd"},
                {"$.diagnostics.hostdisplayname","MACHINE"},
                {"$.diagnostics.license.expired","false"},
                {"NServiceBus.InvokedSagas","MySaga:da6bb5a2-9b3a-4ef2-8c7b-a42d00a86aa5"},
                {"NServiceBus.IsSagaTimeoutMessage","true"}
            };

            var messageId = Guid.NewGuid().ToString();
            const string messageType = "Message1";

            var message = CaptureSagaStateBehavior.BuildSagaChangeInitiatorMessage(headers, messageId, messageType);

            Assert.That(message, Is.Not.Null);
            Assert.That(message.OriginatingEndpoint, Is.Not.Null);
            Assert.That(message.OriginatingMachine, Is.Not.Null);
            Assert.IsNotEmpty(message.OriginatingEndpoint);
            Assert.IsNotEmpty(message.OriginatingMachine);
            Assert.That(message.IsSagaTimeoutMessage, Is.True);
            Assert.That(message.TimeSent, Is.Not.EqualTo(DateTime.MinValue)); // When SC can handle null TimeSent, then should be asserting to null, instead of checking for minValue
        }
    }
}