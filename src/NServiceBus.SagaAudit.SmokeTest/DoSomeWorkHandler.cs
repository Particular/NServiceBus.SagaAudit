namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using System.Threading;
    using Logging;
    using NServiceBus;

    class DoSomeWorkHandler : IHandleMessages<DoSomeWork>
    {
        readonly IBus bus;
        static ILog Log = LogManager.GetLogger<DoSomeWorkHandler>();

        public DoSomeWorkHandler(IBus bus)
        {
            this.bus = bus;
        }

        public void Handle(DoSomeWork message)
        {
            Log.Info($"DoSomeWorkHandler handling message for {message.Identifier}");

            Thread.Sleep(2000);
            bus.Publish(new SomeWorkIsComplete
                {
                    Identifier = message.Identifier,
                    CompletedOn = DateTime.UtcNow
                });
        }
    }
}