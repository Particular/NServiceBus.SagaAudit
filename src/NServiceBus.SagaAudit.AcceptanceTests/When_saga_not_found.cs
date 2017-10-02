namespace ServiceControl.Plugin.Nsb6.SagaAudit.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using EndpointPlugin.Messages.SagaState;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Sagas;
    using NUnit.Framework;

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
                public Context TestContext { get; set; }

                public Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
                {
                    TestContext.Received = true;
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
                    var receiverEndpoint = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));

                    c.SagaPlugin(receiverEndpoint);
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
                    mapper.ConfigureMapping<NotSent>(message => message.MessageId).ToSaga(saga => saga.MessageId);
                    mapper.ConfigureMapping<MessageToBeAudited>(message => message.MessageId).ToSaga(saga => saga.MessageId);
                }
            }

            public class SagaNotFound : IHandleSagaNotFound
            {
                public Context TestContext { get; set; }

                public Task Handle(object message, IMessageProcessingContext context)
                {
                    TestContext.Done = true;
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