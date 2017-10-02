namespace ServiceControl.Plugin.Nsb6.SagaAudit.SmokeTest
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;

    class DoSomeWorkHandler : IHandleMessages<DoSomeWork>
    {
        static ILog Log = LogManager.GetLogger<DoSomeWorkHandler>();

        public Task Handle(DoSomeWork message, IMessageHandlerContext context)
        {
            Log.Info($"DoSomeWorkHandler handling message for {message.Identifier}");

            return Task.Delay(TimeSpan.FromSeconds(2))
                .ContinueWith(task => context.Publish(new SomeWorkIsComplete
                {
                    Identifier = message.Identifier,
                    CompletedOn = DateTime.UtcNow
                }));
        }
    }
}