namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using System.Threading.Tasks;
    using Logging;
    using NServiceBus;

    class MasterSaga : Saga<MasterSagaData>,
        IAmStartedByMessages<StartMasterAlpha>,
        IAmStartedByMessages<StartMasterBeta>,
        IHandleTimeouts<MasterTimedOut>,
        IHandleMessages<WorkRequestedAt>,
        IHandleMessages<ChildFinished>
    {
        static ILog Log = LogManager.GetLogger<MasterSaga>();

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MasterSagaData> mapper)
        {
            mapper.MapSaga(s => s.Identifier)
                .ToMessage<StartMasterAlpha>(msg => msg.Identifier)
                .ToMessage<StartMasterBeta>(msg => msg.Identifier)
                .ToMessage<WorkRequestedAt>(msg => msg.Identifier)
                .ToMessage<ChildFinished>(msg => msg.Identifier);
        }

        public Task Handle(StartMasterAlpha message, IMessageHandlerContext context)
        {
            Data.Identifier = message.Identifier;
            Data.AlphaReceived = true;
            Data.WorkRequired = message.WorkRequired;

            if (!Data.BetaReceived)
            {
                Data.StartedAt = DateTime.UtcNow;
                Log.Info($"Master {Data.Identifier} started with Alpha.");
            }

            return CheckIfReadyToStart(context);
        }

        public Task Handle(StartMasterBeta message, IMessageHandlerContext context)
        {
            Data.Identifier = message.Identifier;
            Data.BetaReceived = true;

            if (!Data.AlphaReceived)
            {
                Data.StartedAt = DateTime.UtcNow;
                Log.Info($"Master {Data.Identifier} started with Beta.");
            }

            return CheckIfReadyToStart(context);
        }

        Task CheckIfReadyToStart(IMessageHandlerContext context)
        {
            if (Data.AlphaReceived && Data.BetaReceived)
            {
                return Task.WhenAll(
                    context.SendLocal(new StartChild
                    {
                        Identifier = Data.Identifier,
                        WorkRequired = Data.WorkRequired
                    }),
                    RequestTimeout<MasterTimedOut>(context, DateTimeOffset.UtcNow.AddSeconds(2))
                    );
            }
            return Task.CompletedTask;
        }

        public Task Timeout(MasterTimedOut state, IMessageHandlerContext context)
        {
            var checkTime = Data.LastWorkRequestedAt ?? Data.StartedAt;

            if (checkTime.AddSeconds(10) > DateTime.UtcNow)
            {
                Log.Warn($"Master Saga {Data.Identifier} Timed Out");
            }

            return RequestTimeout<MasterTimedOut>(context, DateTimeOffset.UtcNow.AddSeconds(10));
        }

        public Task Handle(WorkRequestedAt message, IMessageHandlerContext context)
        {
            Data.LastWorkRequestedAt = message.RequestedAt;

            return Task.CompletedTask;
        }

        public Task Handle(ChildFinished message, IMessageHandlerContext context)
        {
            MarkAsComplete();

            Log.Info($"Master {Data.Identifier} completed.");

            return context.Publish(new MasterFinished
            {
                Identifier = Data.Identifier,
                FinishedAt = DateTime.UtcNow
            });
        }
    }
}