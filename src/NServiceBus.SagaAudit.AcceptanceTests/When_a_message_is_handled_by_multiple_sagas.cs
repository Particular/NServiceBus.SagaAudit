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
using ServiceControl.EndpointPlugin.Messages.SagaState;

public class When_a_message_is_handled_by_multiple_sagas : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_add_saga_headers_to_the_audit_message_only()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<AuditSpy>()
            .WithEndpoint<FakeServiceControl>()
            .WithEndpoint<EndpointWithSagas>(b => b.When((session, ctx) => session.SendLocal(new StartSagas { Id = ctx.TestRunId })))
            .Done(c => c.AuditedHeaders is not null && c.SagaUpdatesReceived >= 2)
            .Run();

        var expectedInvokedSagas = new[]
        {
            $"{typeof(EndpointWithSagas.FirstSaga).FullName}:{context.FirstSagaId}",
            $"{typeof(EndpointWithSagas.SecondSaga).FullName}:{context.SecondSagaId}"
        };
        var expectedStateChanges = new[]
        {
            $"{context.FirstSagaId}:New",
            $"{context.SecondSagaId}:New"
        };

        Assert.Multiple(() =>
        {
            Assert.That(context.FirstSagaId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(context.SecondSagaId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(context.AuditedHeaders["NServiceBus.InvokedSagas"].Split(';'), Is.EquivalentTo(expectedInvokedSagas));
            Assert.That(context.AuditedHeaders["ServiceControl.SagaStateChange"].Split(';'), Is.EquivalentTo(expectedStateChanges));
            Assert.That(context.HeadersSeenAfterHandlers, Does.Not.ContainKey("NServiceBus.InvokedSagas"));
            Assert.That(context.HeadersSeenAfterHandlers, Does.Not.ContainKey("ServiceControl.SagaStateChange"));
        });
    }

    class StartSagas : ICommand
    {
        public Guid Id { get; set; }
    }

    class Context : ScenarioContext
    {
        public Guid FirstSagaId { get; set; }
        public Guid SecondSagaId { get; set; }
        public int SagaUpdatesReceived => sagaUpdatesReceived;
        public void SagaUpdateReceived() => Interlocked.Increment(ref sagaUpdatesReceived);
        int sagaUpdatesReceived;
        public IReadOnlyDictionary<string, string> AuditedHeaders { get; set; } = null!;
        public IReadOnlyDictionary<string, string> HeadersSeenAfterHandlers { get; set; } = null!;
    }

    class EndpointWithSagas : EndpointConfigurationBuilder
    {
        public EndpointWithSagas() =>
            EndpointSetup<DefaultServer>((config, runDescriptor) =>
            {
                config.AuditSagaStateChanges(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl)));
                config.AuditProcessedMessagesTo(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(AuditSpy)));
                config.Pipeline.Register("CaptureHeadersAfterHandlers", new CaptureHeadersAfterHandlersBehavior((Context)runDescriptor.ScenarioContext), "Captures the incoming headers after the handlers ran");
            });

        class CaptureHeadersAfterHandlersBehavior(Context testContext) : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
        {
            public async Task Invoke(IIncomingPhysicalMessageContext context, Func<IIncomingPhysicalMessageContext, Task> next)
            {
                await next(context).ConfigureAwait(false);

                testContext.HeadersSeenAfterHandlers = new Dictionary<string, string>(context.Message.Headers);
            }
        }

        public class FirstSaga(Context testContext) : Saga<FirstSaga.FirstSagaData>, IAmStartedByMessages<StartSagas>
        {
            public Task Handle(StartSagas message, IMessageHandlerContext context)
            {
                Data.TestRunId = message.Id;
                testContext.FirstSagaId = Data.Id;
                return Task.CompletedTask;
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<FirstSagaData> mapper) => mapper.MapSaga(s => s.TestRunId).ToMessage<StartSagas>(m => m.Id);

            public class FirstSagaData : ContainSagaData
            {
                public Guid TestRunId { get; set; }
            }
        }

        public class SecondSaga(Context testContext) : Saga<SecondSaga.SecondSagaData>, IAmStartedByMessages<StartSagas>
        {
            public Task Handle(StartSagas message, IMessageHandlerContext context)
            {
                Data.TestRunId = message.Id;
                testContext.SecondSagaId = Data.Id;
                return Task.CompletedTask;
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SecondSagaData> mapper) => mapper.MapSaga(s => s.TestRunId).ToMessage<StartSagas>(m => m.Id);

            public class SecondSagaData : ContainSagaData
            {
                public Guid TestRunId { get; set; }
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
                if (context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var types) && types.Contains(nameof(StartSagas)))
                {
                    testContext.AuditedHeaders = new Dictionary<string, string>(context.Headers);
                }

                return Task.CompletedTask;
            }
        }

        public class StartSagasHandler : IHandleMessages<StartSagas>
        {
            public Task Handle(StartSagas message, IMessageHandlerContext context) => Task.CompletedTask;
        }
    }

    class FakeServiceControl : EndpointConfigurationBuilder
    {
        public FakeServiceControl()
        {
            IncludeType<SagaUpdatedMessage>();
            EndpointSetup<DefaultServer>();
        }

        public class SagaUpdatedMessageHandler(Context testContext) : IHandleMessages<SagaUpdatedMessage>
        {
            public Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
            {
                testContext.SagaUpdateReceived();
                return Task.CompletedTask;
            }
        }
    }
}
