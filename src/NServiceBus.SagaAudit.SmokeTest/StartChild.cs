namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using NServiceBus;

    class StartChild : ICommand
    {
        public Guid Identifier { get; set; }
        public int WorkRequired { get; set; }
    }
}