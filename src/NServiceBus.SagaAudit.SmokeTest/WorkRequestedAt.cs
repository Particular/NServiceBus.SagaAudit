namespace ServiceControl.Plugin.Nsb6.SagaAudit.SmokeTest
{
    using System;
    using NServiceBus;

    class WorkRequestedAt : IMessage
    {
        public Guid Identifier { get; set; }
        public DateTime RequestedAt { get; set; }
    }
}