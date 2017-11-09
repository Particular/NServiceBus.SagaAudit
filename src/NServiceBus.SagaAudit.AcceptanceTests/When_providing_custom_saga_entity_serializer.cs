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

    public class When_providing_custom_saga_entity_serializer : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_use_it()
        {
            var contextId = Guid.NewGuid();
            var context = await Scenario.Define<Context>()
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
            Assert.AreEqual($"{{\"MyPropertyName\":\"{contextId:N}\"}}", changeMessage.SagaState);
        }

        class Context : ScenarioContext
        {
            internal List<SagaUpdatedMessage> MessagesReceived { get; } = new List<SagaUpdatedMessage>();
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    var receiverEndpoint = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));
                    config.AuditSagaStateChanges(receiverEndpoint, e =>
                    {
                        var typedEntity = (MySaga.MySagaData)e;

                        var result = new Dictionary<string, string>
                        {
                            ["MyPropertyName"] = typedEntity.DataId.ToString("N")
                        };
                        return result;
                    });
                    config.EnableFeature<TimeoutManager>();
                });
            }

            public class MySaga : Saga<MySaga.MySagaData>, IAmStartedByMessages<StartSaga>
            {
                public Context TestContext { get; set; }

                public Task Handle(StartSaga message, IMessageHandlerContext context)
                {
                    Data.DataId = message.DataId;
                    return Task.FromResult(0);
                }
                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                    mapper.ConfigureMapping<StartSaga>(m => m.DataId).ToSaga(s => s.DataId);
                }

                public class MySagaData : ContainSagaData
                {
                    public virtual Guid DataId { get; set; }
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
