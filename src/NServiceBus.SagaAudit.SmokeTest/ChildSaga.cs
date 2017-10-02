namespace ServiceControl.Plugin.Nsb6.SagaAudit.SmokeTest
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;

    class ChildSaga : Saga<ChildSagaData>,
        IAmStartedByMessages<StartChild>,
        IHandleMessages<SomeWorkIsComplete>
    {
        static ILog Log = LogManager.GetLogger<ChildSaga>();

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<ChildSagaData> mapper)
        {
            mapper.ConfigureMapping<StartChild>(msg => msg.Identifier).ToSaga(saga => saga.Identifier);
            mapper.ConfigureMapping<SomeWorkIsComplete>(msg => msg.Identifier).ToSaga(saga => saga.Identifier);
        }


        public Task Handle(StartChild message, IMessageHandlerContext context)
        {
            Data.Identifier = message.Identifier;
            Data.WhenStarted = DateTime.UtcNow;
            Data.WorkRequired = message.WorkRequired;

            Log.InfoFormat($"Child {Data.Identifier} started");

            return RequestWork(context);
        }

        public Task Handle(SomeWorkIsComplete message, IMessageHandlerContext context)
        {
            Data.WorkCompleted++;
            return CheckIfCompleted(context);
        }

        Task RequestWork(IMessageHandlerContext context)
        {
            Log.InfoFormat($"Child {Data.Identifier} requesting work");

            return Task.WhenAll(new []
            {
                context.SendLocal(new DoSomeWork
                {
                    Identifier = Data.Identifier
                }),
                context.Reply(new WorkRequestedAt
                {
                    Identifier = Data.Identifier,
                    RequestedAt = DateTime.UtcNow
                })
            });
        }

        Task CheckIfCompleted(IMessageHandlerContext context)
        {
            if (Data.WorkCompleted < Data.WorkRequired)
            {
                Log.InfoFormat($"Child {Data.Identifier} work not completed {Data.WorkCompleted}/{Data.WorkRequired}");
                return RequestWork(context);
            }

            MarkAsComplete();

            Log.InfoFormat($"Child {Data.Identifier} completed");

            return context.Publish(new ChildFinished
            {
                Identifier = Data.Identifier
            });
        }
    }
}