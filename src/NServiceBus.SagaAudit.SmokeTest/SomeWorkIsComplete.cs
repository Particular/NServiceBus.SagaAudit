namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using NServiceBus;

    class SomeWorkIsComplete : IEvent
    {
        public Guid Identifier { get; set; }
        public DateTime CompletedOn { get; set; }
    }
}