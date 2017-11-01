namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using Saga;

    class MasterSagaData : ContainSagaData
    {
        public Guid Identifier { get; set; }
        public DateTime? LastWorkRequestedAt { get; set; }
        public DateTime StartedAt { get; set; }
        public bool AlphaReceived { get; set; }
        public bool BetaReceived { get; set; }
        public int WorkRequired { get; set; }
    }
}