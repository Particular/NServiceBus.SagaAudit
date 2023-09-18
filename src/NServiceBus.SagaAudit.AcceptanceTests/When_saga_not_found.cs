namespace NServiceBus.SagaAudit.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NUnit.Framework;
    using Sagas;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    public class When_saga_not_found : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_skip_auditing()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<FakeServiceControl>()
                .WithEndpoint<EndpointWithASaga>(b => b.When((messageSession, ctx) =>
                        messageSession.SendLocal(new MessageToBeAudited())
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
                Context testContext;
                public SagaUpdatedMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
                {
                    testContext.Received = true;
                    return Task.FromResult(0);
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
                public Task Handle(NotSent message, IMessageHandlerContext context)
                {
                    throw new NotImplementedException();
                }

                public Task Handle(MessageToBeAudited message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }

                public class MyData : ContainSagaData
                {
                    public virtual Guid MessageId { get; set; }
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MyData> mapper)
                {
                    mapper.MapSaga(s => s.MessageId)
                        .ToMessage<NotSent>(message => message.MessageId)
                        .ToMessage<MessageToBeAudited>(message => message.MessageId);
                }
            }

            public class SagaNotFound : IHandleSagaNotFound
            {
                Context testContext;
                public SagaNotFound(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(object message, IMessageProcessingContext context)
                {
                    testContext.Done = true;
                    return Task.FromResult(0);
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