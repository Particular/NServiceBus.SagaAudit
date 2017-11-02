namespace NServiceBus.SagaAudit.AcceptanceTests
{
    using System;
    using System.Linq;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;
    using Saga;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    class When_a_saga_results_in_a_message
    {
        [Test]
        public void Messages_ent_should_be_captured()
        {
            var contextId = Guid.NewGuid();
            var context = Scenario.Define(new Context()
                {
                    Id = contextId
                })
                .WithEndpoint<FakeServiceControl>()
                .WithEndpoint<Sender>(b => b.When(session =>
                {
                    session.SendLocal(new StartSaga
                    {
                        DataId = contextId
                    });
                }))
                .Done(c => c.WasStarted && c.CommandHandled && c.EventHandled && c.SagaUpdateMessageReceived)
                .Run();

            var sagaupdate = context.SagaUpdatedMessage;

            Assert.IsNotNull(sagaupdate.ResultingMessages.SingleOrDefault(m => m.Intent == MessageIntentEnum.Send.ToString()), "Send messages not single or not found");
            Assert.AreEqual(typeof(TestCommand).ToString(), sagaupdate.ResultingMessages.Single(m => m.Intent == MessageIntentEnum.Send.ToString()).MessageType, "Wrong Send type");
            Assert.IsNotNull(sagaupdate.ResultingMessages.SingleOrDefault(m => m.Intent == MessageIntentEnum.Publish.ToString()), "Publish not single or not found");
            Assert.AreEqual(typeof(TestEvent).ToString(), sagaupdate.ResultingMessages.Single(m => m.Intent == MessageIntentEnum.Publish.ToString()).MessageType, "Wrong Publish type");
        }

        class Context : ScenarioContext
        {
            public Guid Id { get; set; }
            public bool WasStarted { get; set; }
            public bool CommandHandled { get; set; }
            public bool EventHandled { get; set; }
            public bool SagaUpdateMessageReceived { get; set; }
            public SagaUpdatedMessage SagaUpdatedMessage { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    var receiverEndpoint = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));
                    config.AuditSagaStateChanges(receiverEndpoint);
                }).AddMapping<TestEvent>(typeof(Sender));
            }

            public class MySaga : Saga<MySaga.MySagaData>,
                                        IAmStartedByMessages<StartSaga>
            {
                public Context TestContext { get; set; }

                public void Handle(StartSaga message)
                {
                    TestContext.WasStarted = true;
                    Data.DataId = message.DataId;

                    Bus.SendLocal(new TestCommand());
                    Bus.Publish(new TestEvent());

                    MarkAsComplete();
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

            public class TestCommandHandler : IHandleMessages<TestCommand>
            {
                public Context TestContext { get; set; }
                public void Handle(TestCommand message)
                {
                    TestContext.CommandHandled = true;
                }
            }

            public class TestEventHandler : IHandleMessages<TestEvent>
            {
                public Context TestContext { get; set; }

                public void Handle(TestEvent message)
                {
                    TestContext.EventHandled = true;
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

                public void Handle(SagaUpdatedMessage message)
                {
                    TestContext.SagaUpdateMessageReceived = true;
                    TestContext.SagaUpdatedMessage = message;
                }
            }
        }

        public class StartSaga : IMessage
        {
            public Guid DataId { get; set; }
        }

        public class TestCommand : ICommand
        { }

        public class TestEvent : IEvent
        { }
    }
}
