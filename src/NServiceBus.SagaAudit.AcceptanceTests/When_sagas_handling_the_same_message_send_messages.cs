namespace NServiceBus.SagaAudit.AcceptanceTests;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;
using ServiceControl.EndpointPlugin.Messages.SagaState;

public class When_sagas_handling_the_same_message_send_messages : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Each_saga_should_capture_only_its_own_messages()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<FakeServiceControl>()
            .WithEndpoint<EndpointWithSagas>(b => b.When((session, ctx) => session.SendLocal(new StartSagas { Id = ctx.TestRunId })))
            .Done(c => c.SagaUpdates.Count == 2 && c.OutputMessagesHandled == 3)
            .Run();

        var endpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(EndpointWithSagas));
        var firstSagaUpdate = context.SagaUpdates.Single(m => m.SagaType == typeof(EndpointWithSagas.FirstSaga).FullName);
        var secondSagaUpdate = context.SagaUpdates.Single(m => m.SagaType == typeof(EndpointWithSagas.SecondSaga).FullName);

        Assert.Multiple(() =>
        {
            Assert.That(firstSagaUpdate.ResultingMessages.Select(m => m.MessageType), Is.EqualTo([typeof(FirstSagaOutput).ToString()]));
            Assert.That(firstSagaUpdate.ResultingMessages.Single().Destination, Is.EqualTo(endpointName));
            Assert.That(secondSagaUpdate.ResultingMessages.Select(m => m.MessageType), Is.EqualTo([typeof(SecondSagaOutput).ToString()]));
            Assert.That(secondSagaUpdate.ResultingMessages.Single().Destination, Is.EqualTo(endpointName));
        });
    }

    class StartSagas : ICommand
    {
        public Guid Id { get; set; }
    }

    class FirstSagaOutput : ICommand;

    class SecondSagaOutput : ICommand;

    class NonSagaOutput : ICommand;

    class Context : ScenarioContext
    {
        public ConcurrentQueue<SagaUpdatedMessage> SagaUpdates { get; } = new();
        public int OutputMessagesHandled => outputMessagesHandled;
        public void OutputMessageHandled() => Interlocked.Increment(ref outputMessagesHandled);
        int outputMessagesHandled;
    }

    class EndpointWithSagas : EndpointConfigurationBuilder
    {
        public EndpointWithSagas() =>
            EndpointSetup<DefaultServer>(config =>
                config.AuditSagaStateChanges(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl))));

        public class FirstSaga : Saga<FirstSaga.FirstSagaData>, IAmStartedByMessages<StartSagas>
        {
            public Task Handle(StartSagas message, IMessageHandlerContext context)
            {
                Data.TestRunId = message.Id;
                return context.SendLocal(new FirstSagaOutput());
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<FirstSagaData> mapper) => mapper.MapSaga(s => s.TestRunId).ToMessage<StartSagas>(m => m.Id);

            public class FirstSagaData : ContainSagaData
            {
                public Guid TestRunId { get; set; }
            }
        }

        public class SecondSaga : Saga<SecondSaga.SecondSagaData>, IAmStartedByMessages<StartSagas>
        {
            public Task Handle(StartSagas message, IMessageHandlerContext context)
            {
                Data.TestRunId = message.Id;
                return context.SendLocal(new SecondSagaOutput());
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SecondSagaData> mapper) => mapper.MapSaga(s => s.TestRunId).ToMessage<StartSagas>(m => m.Id);

            public class SecondSagaData : ContainSagaData
            {
                public Guid TestRunId { get; set; }
            }
        }

        public class NonSagaHandler : IHandleMessages<StartSagas>
        {
            public Task Handle(StartSagas message, IMessageHandlerContext context) => context.SendLocal(new NonSagaOutput());
        }

        public class FirstSagaOutputHandler(Context testContext) : IHandleMessages<FirstSagaOutput>
        {
            public Task Handle(FirstSagaOutput message, IMessageHandlerContext context)
            {
                testContext.OutputMessageHandled();
                return Task.CompletedTask;
            }
        }

        public class SecondSagaOutputHandler(Context testContext) : IHandleMessages<SecondSagaOutput>
        {
            public Task Handle(SecondSagaOutput message, IMessageHandlerContext context)
            {
                testContext.OutputMessageHandled();
                return Task.CompletedTask;
            }
        }

        public class NonSagaOutputHandler(Context testContext) : IHandleMessages<NonSagaOutput>
        {
            public Task Handle(NonSagaOutput message, IMessageHandlerContext context)
            {
                testContext.OutputMessageHandled();
                return Task.CompletedTask;
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

        public class SagaUpdatedMessageHandler(Context testContext) : IHandleMessages<SagaUpdatedMessage>
        {
            public Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
            {
                testContext.SagaUpdates.Enqueue(message);
                return Task.CompletedTask;
            }
        }
    }
}
