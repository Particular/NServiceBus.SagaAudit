namespace NServiceBus.SagaAudit.AcceptanceTests
{
    using System;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using Saga;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    public class When_saga_not_found : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_skip_auditing()
        {
            var context =  Scenario.Define<Context>()
                .WithEndpoint<FakeServiceControl>()
                .WithEndpoint<EndpointWithASaga>(b => b.When(bus =>
                        bus.SendLocal(new MessageToBeAudited())
                ))
                .Done(c => c.Done)
                .Run();

            Assert.IsFalse(context.Received);
            Assert.IsTrue(context.Done);
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
                    TestContext.Received = true;
                }
            }
        }

        class MessageToBeAudited : ICommand
        {
            public Guid MessageId { get; set; }
        }

        class NotSent : ICommand
        {
            public Guid MessageId { get; set; }
        }

        class EndpointWithASaga : EndpointConfigurationBuilder
        {
            public EndpointWithASaga()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var receiverEndpoint = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));
                    c.AuditSagaStateChanges(receiverEndpoint);
                });
            }

            public class NotStartableSaga : Saga<NotStartableSaga.MyData>, IAmStartedByMessages<NotSent>, IHandleMessages<MessageToBeAudited>
            {
                public void Handle(NotSent message)
                {
                    throw new NotImplementedException();
                }

                public void Handle(MessageToBeAudited message)
                {
                }

                public class MyData : ContainSagaData
                {
                    public virtual Guid MessageId { get; set; }
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MyData> mapper)
                {
                    mapper.ConfigureMapping<NotSent>(message => message.MessageId).ToSaga(saga => saga.MessageId);
                    mapper.ConfigureMapping<MessageToBeAudited>(message => message.MessageId).ToSaga(saga => saga.MessageId);
                }
            }

            public class SagaNotFound : IHandleSagaNotFound
            {
                public Context TestContext { get; set; }

                public void Handle(object message)
                {
                    TestContext.Done = true;
                }
            }
        }

        class Context : ScenarioContext
        {
            public bool Done { get; set; }
            public bool Received { get; set; }
        }
    }
}