namespace NServiceBus.SagaAudit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using Saga;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    public class When_a_message_is_handled_by_a_saga : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_populate_InvokedSagas_header_for_single_saga_audits()
        {
            var context = Scenario.Define<Context>()
                .WithEndpoint<FakeServiceControl>()
                .WithEndpoint<EndpointWithASaga>(b => b.When(bus =>
                    bus.SendLocal(new MessageToBeAudited
                    {
                        Id = Guid.NewGuid()
                    })))
                .Done(c => c.MessageAudited)
                .Run();

            Assert.IsTrue(context.Headers.TryGetValue("NServiceBus.InvokedSagas", out var invokedSagasHeaderValue), "InvokedSagas header is missing");
            Assert.AreEqual($"{typeof(EndpointWithASaga.TheEndpointsSaga).FullName}:{context.SagaId}", invokedSagasHeaderValue);
        }

        [Test]
        public void Should_populate_InvokedSagas_header_for_multiple_saga_audits()
        {
            var context = Scenario.Define<Context>()
                .WithEndpoint<FakeServiceControl>()
                .WithEndpoint<EndpointWithASaga>(b => b.When(bus =>
                    bus.SendLocal(new MessageToBeAuditedByMultiple
                    {
                        Id = Guid.NewGuid()
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
            public IDictionary<string, string> Headers { get; set; }
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
                }).AuditTo<FakeServiceControl>();
            }

            public class TheEndpointsSaga : Saga<TheEndpointsSagaData>, IAmStartedByMessages<MessageToBeAudited>, IAmStartedByMessages<MessageToBeAuditedByMultiple>
            {
                public Context TestContext { get; set; }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TheEndpointsSagaData> mapper)
                {
                    mapper.ConfigureMapping<MessageToBeAudited>(msg => msg.Id).ToSaga(saga => saga.TestRunId);
                    mapper.ConfigureMapping<MessageToBeAuditedByMultiple>(msg => msg.Id).ToSaga(saga => saga.TestRunId);
                }


                public void Handle(MessageToBeAudited message)
                {
                    Data.TestRunId = message.Id;
                    TestContext.SagaId = Data.Id;
                }

                public void Handle(MessageToBeAuditedByMultiple message)
                {
                    Data.TestRunId = message.Id;
                    TestContext.SagaId = Data.Id;
                }
            }

            public class TheEndpointsSagaAlternative : Saga<TheEndpointsSagaAlternativeData>, IAmStartedByMessages<MessageToBeAuditedByMultiple>
            {
                public Context TestContext { get; set; }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TheEndpointsSagaAlternativeData> mapper)
                {
                    mapper.ConfigureMapping<MessageToBeAuditedByMultiple>(msg => msg.Id).ToSaga(saga => saga.TestRunId);
                }

                public void Handle(MessageToBeAuditedByMultiple message)
                {
                    Data.TestRunId = message.Id;
                    TestContext.AlternativeSagaId = Data.Id;
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
                public void Handle(SagaUpdatedMessage message)
                {
                }
            }

            public class MessageToBeAuditedHandler : IHandleMessages<MessageToBeAudited>, IHandleMessages<MessageToBeAuditedByMultiple>
            {
                public Context TestContext { get; set; }
                public IBus Bus { get; set; }

                public void Handle(MessageToBeAudited message)
                {
                    TestContext.MessageAudited = true;
                    TestContext.Headers = Bus.CurrentMessageContext.Headers;
                }

                public void Handle(MessageToBeAuditedByMultiple message)
                {
                    TestContext.MessageAudited = true;
                    TestContext.Headers = Bus.CurrentMessageContext.Headers;
                }
            }
        }
    }
}
