namespace NServiceBus.SagaAudit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using NServiceBus;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    public class When_using_complex_saga_properties : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_exclude_them_in_saga_state()
        {
            var contextId = Guid.NewGuid();
            var context = await Scenario.Define<Context>(c => { c.Id = contextId; })
                .WithEndpoint<FakeServiceControl>()
                .WithEndpoint<Sender>(b => b.When(session =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.RouteToThisEndpoint();
                    return session.Send(new StartSaga
                    {
                        DataId = contextId
                    }, sendOptions);
                }))
                .Done(c => c.MessagesReceived.Count == 1)
                .Run();

            var changeMessage = context.MessagesReceived.First(msg => msg?.Initiator?.MessageType == typeof(StartSaga).FullName);
            Assert.AreEqual($"{{\"DataId\":\"{contextId}\",\"Id\":\"{context.SagaId}\",\"Originator\":\"UsingComplexSagaProperties.Sender\",\"OriginalMessageId\":\"{context.OriginalMessageId}\",\"$type\":\"NServiceBus.SagaAudit.AcceptanceTests.When_using_complex_saga_properties+Sender+MySaga+MySagaData, NServiceBus.SagaAudit.AcceptanceTests\"}}", changeMessage.SagaState);
        }

        class Context : ScenarioContext
        {
            public Guid Id { get; set; }
            internal List<SagaUpdatedMessage> MessagesReceived { get; } = new List<SagaUpdatedMessage>();
            public Guid SagaId { get; set; }
            public string OriginalMessageId { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    var receiverEndpoint = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));
                    config.AuditSagaStateChanges(receiverEndpoint);
                    config.EnableFeature<TimeoutManager>();
                });
            }

            public class MySaga : Saga<MySaga.MySagaData>, IAmStartedByMessages<StartSaga>
            {
                public Context TestContext { get; set; }

                public Task Handle(StartSaga message, IMessageHandlerContext context)
                {
                    Data.DataId = message.DataId;
                    Data.ComplexNestedObject = new NestedObject
                    {
                        Value = "Some value"
                    };
                    TestContext.OriginalMessageId = Data.OriginalMessageId;
                    TestContext.SagaId = Data.Id;
                    return Task.FromResult(0);
                }
                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                    mapper.ConfigureMapping<StartSaga>(m => m.DataId).ToSaga(s => s.DataId);
                }

                public class MySagaData : ContainSagaData
                {
                    public virtual Guid DataId { get; set; }
                    public NestedObject ComplexNestedObject { get; set; }
                }

                public class NestedObject
                {
                    public virtual string Value { get; set; }
                }
            }
        }

        class FakeServiceControl : EndpointConfigurationBuilder
        {
            public FakeServiceControl()
            {
                IncludeType<SagaUpdatedMessage>();

                EndpointSetup<DefaultServer>();
            }

            public class SagaUpdatedMessageHandler : IHandleMessages<SagaUpdatedMessage>
            {
                public Context TestContext { get; set; }

                public Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
                {
                    TestContext.MessagesReceived.Add(message);
                    return Task.FromResult(0);
                }
            }
        }

        public class StartSaga : IMessage
        {
            public Guid DataId { get; set; }
        }
    }
}
