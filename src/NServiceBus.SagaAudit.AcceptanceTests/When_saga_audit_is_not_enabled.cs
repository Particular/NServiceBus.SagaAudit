namespace NServiceBus.SagaAudit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    public class When_saga_audit_is_not_enabled : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_not_populate_InvokedSagas_header()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<FakeServiceControl>()
                .WithEndpoint<EndpointWithASaga>(b => b.When((messageSession, ctx) =>
                    messageSession.SendLocal(new MessageToBeAudited
                    {
                        Id = ctx.TestRunId
                    })
                ))
                .Done(c => c.MessageAudited)
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.MessageAudited, Is.True);
                Assert.That(context.Headers.ContainsKey("NServiceBus.InvokedSagas"), Is.False);
            });
        }

        class MessageToBeAudited : ICommand
        {
            public Guid Id { get; set; }
        }

        class Context : ScenarioContext
        {
            public bool MessageAudited { get; set; }
            public IReadOnlyDictionary<string, string> Headers { get; set; }
        }

        class EndpointWithASaga : EndpointConfigurationBuilder
        {
            public EndpointWithASaga()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var receiverEndpoint = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));
                    c.AuditProcessedMessagesTo(receiverEndpoint);
                });
            }

            public class TheEndpointsSaga : Saga<TheEndpointsSagaData>, IAmStartedByMessages<MessageToBeAudited>
            {
                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TheEndpointsSagaData> mapper)
                {
                    mapper.ConfigureMapping<MessageToBeAudited>(msg => msg.Id).ToSaga(saga => saga.TestRunId);
                }


                public Task Handle(MessageToBeAudited message, IMessageHandlerContext context)
                {
                    Data.TestRunId = message.Id;
                    return Task.CompletedTask;
                }
            }

            public class TheEndpointsSagaData : ContainSagaData
            {
                public Guid TestRunId { get; set; }
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
                public Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
                {
                    return Task.CompletedTask;
                }
            }

            public class MessageToBeAuditedHandler : IHandleMessages<MessageToBeAudited>
            {
                Context testContext;
                public MessageToBeAuditedHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MessageToBeAudited message, IMessageHandlerContext context)
                {
                    testContext.Headers = context.MessageHeaders;
                    testContext.MessageAudited = true;
                    return Task.CompletedTask;
                }
            }
        }
    }
}
