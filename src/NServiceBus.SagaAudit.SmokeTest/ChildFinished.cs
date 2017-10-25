namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using NServiceBus;

    class ChildFinished : IEvent
    {
        public Guid Identifier { get; set; }
    }
}