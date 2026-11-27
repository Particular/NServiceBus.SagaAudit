namespace NServiceBus.SagaAudit.AcceptanceTests;

using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public partial class When_serializer_options_have_no_metadata_for_saga_data : NServiceBusAcceptanceTest
{
    [Test]
    public void Should_fail_to_start()
    {
        var exception = Assert.CatchAsync(() => Scenario.Define<ScenarioContext>()
            .WithEndpoint<Sender>()
            .Done(_ => true)
            .Run());

        Assert.That(exception!.ToString(), Does.Contain("No JSON metadata was found for the saga data type").And.Contain(typeof(Sender.MySaga.MySagaData).FullName));
    }

    public class Sender : EndpointConfigurationBuilder
    {
        public Sender() =>
            EndpointSetup<DefaultServer>(config => config.AuditSagaStateChanges("Particular.ServiceControl", UnrelatedJsonContext.Default.Options));

        public class MySaga : Saga<MySaga.MySagaData>, IAmStartedByMessages<StartSaga>
        {
            public Task Handle(StartSaga message, IMessageHandlerContext context) => Task.CompletedTask;

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper) => mapper.MapSaga(s => s.DataId).ToMessage<StartSaga>(m => m.DataId);

            public class MySagaData : ContainSagaData
            {
                public virtual Guid DataId { get; set; }
            }
        }
    }

    [JsonSerializable(typeof(Guid))]
    partial class UnrelatedJsonContext : JsonSerializerContext;

    public class StartSaga : IMessage
    {
        public Guid DataId { get; set; }
    }
}
