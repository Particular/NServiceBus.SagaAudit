namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using Logging;
    using NServiceBus;
    using Saga;

    class ChildSaga : Saga<ChildSagaData>,
        IAmStartedByMessages<StartChild>,
        IHandleMessages<SomeWorkIsComplete>
    {
        readonly IBus bus;
        static ILog Log = LogManager.GetLogger<ChildSaga>();

        public ChildSaga(IBus bus)
        {
            this.bus = bus;
        }

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<ChildSagaData> mapper)
        {
            mapper.ConfigureMapping<StartChild>(msg => msg.Identifier).ToSaga(saga => saga.Identifier);
            mapper.ConfigureMapping<SomeWorkIsComplete>(msg => msg.Identifier).ToSaga(saga => saga.Identifier);
        }


        public void Handle(StartChild message)
        {
            Data.Identifier = message.Identifier;
            Data.WhenStarted = DateTime.UtcNow;
            Data.WorkRequired = message.WorkRequired;

            Log.InfoFormat($"Child {Data.Identifier} started");

            RequestWork();
        }

        public void Handle(SomeWorkIsComplete message)
        {
            Data.WorkCompleted++;
            CheckIfCompleted();
        }

        void RequestWork()
        {
            Log.InfoFormat($"Child {Data.Identifier} requesting work");

            bus.SendLocal(new DoSomeWork
            {
                Identifier = Data.Identifier
            });

            bus.Reply(new WorkRequestedAt
            {
                Identifier = Data.Identifier,
                RequestedAt = DateTime.UtcNow
            });
        }

        void CheckIfCompleted()
        {
            if (Data.WorkCompleted < Data.WorkRequired)
            {
                Log.InfoFormat($"Child {Data.Identifier} work not completed {Data.WorkCompleted}/{Data.WorkRequired}");
                RequestWork();
                return;
            }

            MarkAsComplete();

            Log.InfoFormat($"Child {Data.Identifier} completed");

            bus.Publish(new ChildFinished
            {
                Identifier = Data.Identifier
            });
        }
    }
}