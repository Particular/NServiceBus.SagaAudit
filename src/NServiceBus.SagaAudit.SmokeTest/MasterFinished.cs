namespace ServiceControl.Plugin.Nsb6.SagaAudit.SmokeTest
{
    using System;
    using NServiceBus;

    class MasterFinished : IEvent
    {
        public Guid Identifier { get; set; }
        public DateTime FinishedAt { get; set; }
    }
}