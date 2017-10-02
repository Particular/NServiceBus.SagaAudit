namespace ServiceControl.Plugin.Nsb6.SagaAudit.SmokeTest
{
    using System;
    using NServiceBus;

    class ChildFinished : IEvent
    {
        public Guid Identifier { get; set; }
    }
}