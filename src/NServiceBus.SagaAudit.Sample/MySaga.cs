using NServiceBus.Logging;
using NServiceBus.Saga;

public class MySaga : Saga<MySagaData>, IAmStartedByMessages<Message1>
{
    static ILog logger = LogManager.GetLogger(typeof(MySaga));

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
    {
        mapper.ConfigureMapping<Message1>(s => s.SomeId)
            .ToSaga(m => m.SomeId);
    }

    public void Handle(Message1 message)
    {
        Data.SomeId = message.SomeId;
        logger.Info("Hello from MySaga");
        MarkAsComplete();
        Bus.SendLocal(new Message2());
    }
}