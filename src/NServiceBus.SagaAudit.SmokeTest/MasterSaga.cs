namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using Logging;
    using NServiceBus;
    using Saga;

    class MasterSaga : Saga<MasterSagaData>,
        IAmStartedByMessages<StartMasterAlpha>,
        IAmStartedByMessages<StartMasterBeta>,
        IHandleTimeouts<MasterTimedOut>,
        IHandleMessages<WorkRequestedAt>,
        IHandleMessages<ChildFinished>
    {
        readonly IBus bus;
        static ILog Log = LogManager.GetLogger<MasterSaga>();

        public MasterSaga(IBus bus)
        {
            this.bus = bus;
        }

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MasterSagaData> mapper)
        {
            mapper.ConfigureMapping<StartMasterAlpha>(msg => msg.Identifier).ToSaga(saga => saga.Identifier);
            mapper.ConfigureMapping<StartMasterBeta>(msg => msg.Identifier).ToSaga(saga => saga.Identifier);
            mapper.ConfigureMapping<WorkRequestedAt>(msg => msg.Identifier).ToSaga(saga => saga.Identifier);
            mapper.ConfigureMapping<ChildFinished>(msg => msg.Identifier).ToSaga(saga => saga.Identifier);
        }

        public void Handle(StartMasterAlpha message)
        {
            Data.Identifier = message.Identifier;
            Data.AlphaReceived = true;
            Data.WorkRequired = message.WorkRequired;

            if (!Data.BetaReceived)
            {
                Data.StartedAt = DateTime.UtcNow;
                Log.Info($"Master {Data.Identifier} started with Alpha.");
            }

            CheckIfReadyToStart();
        }

        public void Handle(StartMasterBeta message)
        {
            Data.Identifier = message.Identifier;
            Data.BetaReceived = true;

            if (!Data.AlphaReceived)
            {
                Data.StartedAt = DateTime.UtcNow;
                Log.Info($"Master {Data.Identifier} started with Beta.");
            }

            CheckIfReadyToStart();
        }

        void CheckIfReadyToStart()
        {
            if (Data.AlphaReceived && Data.BetaReceived)
            {
                bus.SendLocal(new StartChild
                {
                    Identifier = Data.Identifier,
                    WorkRequired = Data.WorkRequired
                });
                RequestTimeout<MasterTimedOut>(DateTime.UtcNow.AddSeconds(2));
            }
        }

        public void Timeout(MasterTimedOut state)
        {
            var checkTime = Data.LastWorkRequestedAt ?? Data.StartedAt;

            if (checkTime.AddSeconds(10) > DateTime.UtcNow)
            {
                Log.Warn($"Master Saga {Data.Identifier} Timed Out");
            }

            RequestTimeout<MasterTimedOut>(DateTime.UtcNow.AddSeconds(10));
        }

        public void Handle(WorkRequestedAt message)
        {
            Data.LastWorkRequestedAt = message.RequestedAt;
        }

        public void Handle(ChildFinished message)
        {
            MarkAsComplete();

            Log.Info($"Master {Data.Identifier} completed.");

            bus.Publish(new MasterFinished
            {
                Identifier = Data.Identifier,
                FinishedAt = DateTime.UtcNow
            });
        }
    }
}