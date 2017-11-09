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

            Assert.IsTrue(context.Headers.TryGetValue("NServiceBus.InvokedSagas", out var invokedSagasHeaderValue), "InvokedSagas header is missing");
            Assert.AreEqual($"{typeof(EndpointWithASaga.TheEndpointsSaga).FullName}:{context.SagaId}", invokedSagasHeaderValue);
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

            Assert.IsTrue(context.Headers.TryGetValue("NServiceBus.InvokedSagas", out var invokedSagasHeaderValue), "InvokedSagas header is missing");
            Assert.IsTrue(invokedSagasHeaderValue.Contains($"{typeof(EndpointWithASaga.TheEndpointsSaga).FullName}:{context.SagaId}"), "TheEndpointsSaga header value is missing");
            Assert.IsTrue(invokedSagasHeaderValue.Contains($"{typeof(EndpointWithASaga.TheEndpointsSagaAlternative).FullName}:{context.AlternativeSagaId}"), "TheEndpointsSagaAlternative header value is missing");
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
                public Context TestContext { get; set; }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TheEndpointsSagaData> mapper)
                {
                    mapper.ConfigureMapping<MessageToBeAudited>(msg => msg.Id).ToSaga(saga => saga.TestRunId);
                    mapper.ConfigureMapping<MessageToBeAuditedByMultiple>(msg => msg.Id).ToSaga(saga => saga.TestRunId);
                }


                public Task Handle(MessageToBeAudited message, IMessageHandlerContext context)
                {
                    Data.TestRunId = message.Id;
                    TestContext.SagaId = Data.Id;
                    return Task.FromResult(0);
                }

                public Task Handle(MessageToBeAuditedByMultiple message, IMessageHandlerContext context)
                {
                    Data.TestRunId = message.Id;
                    TestContext.SagaId = Data.Id;
                    return Task.FromResult(0);
                }
            }

            public class TheEndpointsSagaAlternative : Saga<TheEndpointsSagaAlternativeData>, IAmStartedByMessages<MessageToBeAuditedByMultiple>
            {
                public Context TestContext { get; set; }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TheEndpointsSagaAlternativeData> mapper)
                {
                    mapper.ConfigureMapping<MessageToBeAuditedByMultiple>(msg => msg.Id).ToSaga(saga => saga.TestRunId);
                }

                public Task Handle(MessageToBeAuditedByMultiple message, IMessageHandlerContext context)
                {
                    Data.TestRunId = message.Id;
                    TestContext.AlternativeSagaId = Data.Id;
                    return Task.FromResult(0);
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

                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseSerialization<JsonSerializer>();
                });
            }

            public class SagaUpdatedMessageHandler : IHandleMessages<SagaUpdatedMessage>
            {
                public Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }

            public class MessageToBeAuditedHandler : IHandleMessages<MessageToBeAudited>, IHandleMessages<MessageToBeAuditedByMultiple>
            {
                public Context TestContext { get; set; }

                public Task Handle(MessageToBeAudited message, IMessageHandlerContext context)
                {
                    TestContext.MessageAudited = true;
                    TestContext.Headers = context.MessageHeaders;
                    return Task.FromResult(0);
                }

                public Task Handle(MessageToBeAuditedByMultiple message, IMessageHandlerContext context)
                {
                    TestContext.MessageAudited = true;
                    TestContext.Headers = context.MessageHeaders;
                    return Task.FromResult(0);
                }
            }
        }
    }
}
