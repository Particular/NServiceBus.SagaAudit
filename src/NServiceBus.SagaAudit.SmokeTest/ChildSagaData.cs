namespace ServiceControl.Plugin.Nsb6.SagaAudit.SmokeTest
{
    using System;
    using NServiceBus;

    class ChildSagaData : ContainSagaData
    {
        public Guid Identifier { get; set; }

        public DateTime WhenStarted { get; set; }
        public int WorkCompleted { get; set; }
        public int WorkRequired { get; set; }
    }
}