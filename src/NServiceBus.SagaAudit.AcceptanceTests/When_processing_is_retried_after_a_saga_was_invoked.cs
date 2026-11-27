namespace NServiceBus.SagaAudit.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using MessageMutator;
using NServiceBus;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;
using Pipeline;

public class When_processing_is_retried_after_a_saga_was_invoked : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_audit_only_the_saga_entries_of_the_successful_attempt()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<AuditSpy>()
            .WithEndpoint<FakeServiceControl>()
            .WithEndpoint<EndpointWithSaga>(b => b
                .DoNotFailOnErrorMessages()
                .When((session, ctx) => session.SendLocal(new StartSaga { Id = ctx.TestRunId })))
            .Done(c => c.AuditedHeaders is not null)
            .Run();

        var expectedStateChange = context.TimesHandledBySagaData == 1 ? "New" : "Updated";

        Assert.Multiple(() =>
        {
            Assert.That(context.ProcessingAttempts, Is.EqualTo(2), "The message must be retried once");
            Assert.That(context.SagaId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(context.AuditedHeaders["NServiceBus.InvokedSagas"], Is.EqualTo($"{typeof(EndpointWithSaga.TheSaga).FullName}:{context.SagaId}"));
            Assert.That(context.AuditedHeaders["ServiceControl.SagaStateChange"], Is.EqualTo($"{context.SagaId}:{expectedStateChange}"));
        });
    }

    class StartSaga : ICommand
    {
        public Guid Id { get; set; }
    }

    class Context : ScenarioContext
    {
        public int ProcessingAttempts => processingAttempts;
        public int ProcessingAttempted() => Interlocked.Increment(ref processingAttempts);
        int processingAttempts;
        public Guid SagaId { get; set; }
        public int TimesHandledBySagaData { get; set; }
        public IReadOnlyDictionary<string, string> AuditedHeaders { get; set; } = null!;
    }

    class EndpointWithSaga : EndpointConfigurationBuilder
    {
        public EndpointWithSaga() =>
            EndpointSetup<DefaultServer>((config, runDescriptor) =>
            {
                config.Pipeline.Register("FailOnFirstAttempt", new FailOnFirstAttemptBehavior((Context)runDescriptor.ScenarioContext), "Fails processing after the handlers ran on the first attempt");
                config.Recoverability().Immediate(immediate => immediate.NumberOfRetries(1));
                config.AuditSagaStateChanges(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl)));
                config.AuditProcessedMessagesTo(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(AuditSpy)));
            });

        public class TheSaga(Context testContext) : Saga<TheSaga.TheSagaData>, IAmStartedByMessages<StartSaga>
        {
            public Task Handle(StartSaga message, IMessageHandlerContext context)
            {
                Data.TestRunId = message.Id;
                Data.TimesHandled++;
                testContext.SagaId = Data.Id;
                testContext.TimesHandledBySagaData = Data.TimesHandled;
                return Task.CompletedTask;
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TheSagaData> mapper) => mapper.MapSaga(s => s.TestRunId).ToMessage<StartSaga>(m => m.Id);

            public class TheSagaData : ContainSagaData
            {
                public Guid TestRunId { get; set; }
                public int TimesHandled { get; set; }
            }
        }

        class FailOnFirstAttemptBehavior(Context testContext) : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
        {
            public async Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
            {
                await next(context).ConfigureAwait(false);

                if (testContext.ProcessingAttempted() == 1)
                {
                    throw new SimulatedException("Fails after the saga recorded its audit entries");
                }
            }
        }
    }

    class AuditSpy : EndpointConfigurationBuilder
    {
        public AuditSpy() =>
            EndpointSetup<DefaultServer>((config, runDescriptor) => config.RegisterMessageMutator(new CaptureAuditedHeadersMutator((Context)runDescriptor.ScenarioContext)));

        class CaptureAuditedHeadersMutator(Context testContext) : IMutateIncomingTransportMessages
        {
            public Task MutateIncoming(MutateIncomingTransportMessageContext context)
            {
                if (context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var types) && types.Contains(nameof(StartSaga)))
                {
                    testContext.AuditedHeaders = new Dictionary<string, string>(context.Headers);
                }

                return Task.CompletedTask;
            }
        }

        public class StartSagaHandler : IHandleMessages<StartSaga>
        {
            public Task Handle(StartSaga message, IMessageHandlerContext context) => Task.CompletedTask;
        }
    }

    class FakeServiceControl : EndpointConfigurationBuilder
    {
        public FakeServiceControl()
        {
            IncludeType<ServiceControl.EndpointPlugin.Messages.SagaState.SagaUpdatedMessage>();
            EndpointSetup<DefaultServer>();
        }

        public class SagaUpdatedMessageHandler : IHandleMessages<ServiceControl.EndpointPlugin.Messages.SagaState.SagaUpdatedMessage>
        {
            public Task Handle(ServiceControl.EndpointPlugin.Messages.SagaState.SagaUpdatedMessage message, IMessageHandlerContext context) => Task.CompletedTask;
        }
    }
}
