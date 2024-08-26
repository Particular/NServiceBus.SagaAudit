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

    public class When_a_message_is_handled_by_a_saga : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_populate_InvokedSagas_header_for_single_saga_audits()
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

            Assert.That(context.Headers.TryGetValue("NServiceBus.InvokedSagas", out var invokedSagasHeaderValue), Is.True, "InvokedSagas header is missing");
            Assert.That(invokedSagasHeaderValue, Is.EqualTo($"{typeof(EndpointWithASaga.TheEndpointsSaga).FullName}:{context.SagaId}"));
        }

        [Test]
        public async Task Should_populate_InvokedSagas_header_for_multiple_saga_audits()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<FakeServiceControl>()
                .WithEndpoint<EndpointWithASaga>(b => b.When((messageSession, ctx) =>
                    messageSession.SendLocal(new MessageToBeAuditedByMultiple
                    {
                        Id = ctx.TestRunId
                    })
                ))
                .Done(c => c.MessageAudited)
                .Run();

            Assert.That(context.Headers.TryGetValue("NServiceBus.InvokedSagas", out var invokedSagasHeaderValue), Is.True, "InvokedSagas header is missing");
            Assert.That(invokedSagasHeaderValue.Contains($"{typeof(EndpointWithASaga.TheEndpointsSaga).FullName}:{context.SagaId}"), Is.True, "TheEndpointsSaga header value is missing");
            Assert.That(invokedSagasHeaderValue.Contains($"{typeof(EndpointWithASaga.TheEndpointsSagaAlternative).FullName}:{context.AlternativeSagaId}"), Is.True, "TheEndpointsSagaAlternative header value is missing");
        }

        class MessageToBeAudited : ICommand
        {
            public Guid Id { get; set; }
        }

        class MessageToBeAuditedByMultiple : ICommand
        {
            public Guid Id { get; set; }
        }

        class Context : ScenarioContext
        {
            public bool MessageAudited { get; set; }
            public IReadOnlyDictionary<string, string> Headers { get; set; }
            public Guid SagaId { get; set; }
            public Guid AlternativeSagaId { get; set; }
        }

        class EndpointWithASaga : EndpointConfigurationBuilder
        {
            public EndpointWithASaga()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var receiverEndpoint = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));

                    c.AuditSagaStateChanges(receiverEndpoint);
                    c.AuditProcessedMessagesTo(receiverEndpoint);
                });
            }

            public class TheEndpointsSaga : Saga<TheEndpointsSagaData>, IAmStartedByMessages<MessageToBeAudited>, IAmStartedByMessages<MessageToBeAuditedByMultiple>
            {
                Context testContext;
                public TheEndpointsSaga(Context testContext)
                {
                    this.testContext = testContext;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TheEndpointsSagaData> mapper)
                {
                    mapper.MapSaga(s => s.TestRunId)
                        .ToMessage<MessageToBeAudited>(msg => msg.Id)
                        .ToMessage<MessageToBeAuditedByMultiple>(msg => msg.Id);
                }


                public Task Handle(MessageToBeAudited message, IMessageHandlerContext context)
                {
                    Data.TestRunId = message.Id;
                    testContext.SagaId = Data.Id;
                    return Task.CompletedTask;
                }

                public Task Handle(MessageToBeAuditedByMultiple message, IMessageHandlerContext context)
                {
                    Data.TestRunId = message.Id;
                    testContext.SagaId = Data.Id;
                    return Task.CompletedTask;
                }
            }

            public class TheEndpointsSagaAlternative : Saga<TheEndpointsSagaAlternativeData>, IAmStartedByMessages<MessageToBeAuditedByMultiple>
            {
                Context testContext;
                public TheEndpointsSagaAlternative(Context testContext)
                {
                    this.testContext = testContext;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TheEndpointsSagaAlternativeData> mapper)
                {
                    mapper.ConfigureMapping<MessageToBeAuditedByMultiple>(msg => msg.Id).ToSaga(saga => saga.TestRunId);
                }

                public Task Handle(MessageToBeAuditedByMultiple message, IMessageHandlerContext context)
                {
                    Data.TestRunId = message.Id;
                    testContext.AlternativeSagaId = Data.Id;
                    return Task.CompletedTask;
                }
            }

            public class TheEndpointsSagaData : ContainSagaData
            {
                public Guid TestRunId { get; set; }
            }

            public class TheEndpointsSagaAlternativeData : ContainSagaData
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

            public class MessageToBeAuditedHandler : IHandleMessages<MessageToBeAudited>, IHandleMessages<MessageToBeAuditedByMultiple>
            {
                Context testContext;
                public MessageToBeAuditedHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MessageToBeAudited message, IMessageHandlerContext context)
                {
                    testContext.MessageAudited = true;
                    testContext.Headers = context.MessageHeaders;
                    return Task.CompletedTask;
                }

                public Task Handle(MessageToBeAuditedByMultiple message, IMessageHandlerContext context)
                {
                    testContext.MessageAudited = true;
                    testContext.Headers = context.MessageHeaders;
                    return Task.CompletedTask;
                }
            }
        }
    }
}
