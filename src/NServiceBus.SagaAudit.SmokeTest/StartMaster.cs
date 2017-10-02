namespace ServiceControl.Plugin.Nsb6.SagaAudit.SmokeTest
{
    using System;
    using NServiceBus;

    class StartMasterAlpha : ICommand
    {
        public Guid Identifier { get; set; }
        public int WorkRequired { get; set; }
    }

    class StartMasterBeta : ICommand
    {
        public Guid Identifier { get; set; }
    }
}