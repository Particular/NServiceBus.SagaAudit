namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using NServiceBus;

    class DoSomeWork : ICommand
    {
        public Guid Identifier { get; set; }
    }
}