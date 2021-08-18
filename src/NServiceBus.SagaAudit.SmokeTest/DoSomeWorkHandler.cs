namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using System.Threading.Tasks;
    using Logging;
    using NServiceBus;

    class DoSomeWorkHandler : IHandleMessages<DoSomeWork>
    {
        static ILog Log = LogManager.GetLogger<DoSomeWorkHandler>();

        public Task Handle(DoSomeWork message, IMessageHandlerContext context)
        {
            Log.Info($"DoSomeWorkHandler handling message for {message.Identifier}");

            return Task.Delay(TimeSpan.FromSeconds(2), context.CancellationToken)
                .ContinueWith(task => context.Publish(new SomeWorkIsComplete
                {
                    Identifier = message.Identifier,
                    CompletedOn = DateTime.UtcNow
                }));
        }
    }
}