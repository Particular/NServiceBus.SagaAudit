namespace ServiceControl.Plugin.Nsb6.SagaAudit.SmokeTest
{
    using System;
    using NServiceBus;

    class DoSomeWork : ICommand
    {
        public Guid Identifier { get; set; }
    }
}