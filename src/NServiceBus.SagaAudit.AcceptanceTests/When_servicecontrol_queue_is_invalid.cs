namespace NServiceBus.SagaAudit.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NUnit.Framework;

    class When_servicecontrol_queue_is_invalid : NServiceBusAcceptanceTest
    {
        [Test]
        public void The_endpoint_should_not_start()
        {
            var ex = Assert.ThrowsAsync<Exception>(async () => await Scenario.Define<Context>()
                .WithEndpoint<Sender>()
                .Run());

            StringAssert.Contains("You have enabled saga state change auditing in your endpoint, however, this endpoint is unable to contact the ServiceControl to report endpoint information.", ex.Message);
        }

        class Context : ScenarioContext
        {
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c => c.AuditSagaStateChanges(new string(Path.GetInvalidPathChars())));
            }
        }

        public class MySaga : Saga<MySaga.MySagaData>, IAmStartedByMessages<MyMessage>
        {
            public class MySagaData : ContainSagaData
            {
                public string CorrelationId { get; set; }
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
            {
                mapper.ConfigureMapping<MyMessage>(m => m.CorrelationId).ToSaga(s => s.CorrelationId);
            }

            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                return Task.FromResult(0);
            }
        }

        public class MyMessage : IMessage
        {
            public string CorrelationId { get; set; }
        }
    }
}