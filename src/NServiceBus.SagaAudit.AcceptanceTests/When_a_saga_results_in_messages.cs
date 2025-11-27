namespace NServiceBus.SagaAudit.AcceptanceTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;
using ServiceControl.EndpointPlugin.Messages.SagaState;

class When_a_saga_results_in_messages
{
    [Test]
    public async Task Those_messages_should_be_captured()
    {
        var contextId = Guid.NewGuid();
        var context = await Scenario.Define<Context>(ctx => ctx.Id = contextId)
            .WithEndpoint<FakeServiceControl>()
            .WithEndpoint<Endpoint>(b => b.When(session => session.SendLocal(new StartSaga { DataId = contextId })))
            .Done(c => c.WasStarted && c.CommandHandled && c.DelayedByCommandHandled && c.DelayAtCommandHandled && c.EventHandled && c.SagaUpdateMessageReceived)
            .Run();

        var sagaupdate = context.SagaUpdatedMessage;

        var command = sagaupdate.ResultingMessages.SingleOrDefault(m => m.MessageType == typeof(TestCommand).ToString());
        Assert.That(command, Is.Not.Null, "Command messages not single or not found");
        Assert.Multiple(() =>
        {
            Assert.That(command.Intent, Is.EqualTo(MessageIntent.Send.ToString()), "Command intent mismatch");
            Assert.That(command.ResultingMessageId, Is.EqualTo(context.MessageId), "MessageId mismatch");
            Assert.That(Math.Abs((context.TimeSent - command.TimeSent).TotalSeconds), Is.LessThan(1d), "TimeSent mismatch"); //Test within 1 second rounded, since now we have to populate TimeSent with UtcNow as the header is not yet set
            Assert.That(command.Destination, Is.EqualTo(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Endpoint))), "Destination mismatch");
            Assert.That(command.DeliveryDelay, Is.Null, "Command DeliveryDelay");
            Assert.That(command.DeliveryAt, Is.Null, "Command DeliveryAt");
        });

        var delayedByCommand = sagaupdate.ResultingMessages.SingleOrDefault(m => m.MessageType == typeof(TestDelayedByCommand).ToString());
        Assert.That(delayedByCommand, Is.Not.Null, "Delayed by command message not single or not found");
        Assert.That(delayedByCommand.DeliveryDelay, Is.Not.Null, "Delay by Command");

        var delayedAtCommand = sagaupdate.ResultingMessages.SingleOrDefault(m => m.MessageType == typeof(TestDelayAtCommand).ToString());
        Assert.That(delayedAtCommand, Is.Not.Null, "Delayed at command message not single or not found");
        Assert.That(delayedAtCommand.DeliveryAt, Is.Not.Null, "Delivery At Command");

        var @event = sagaupdate.ResultingMessages.SingleOrDefault(m => m.MessageType == typeof(TestEvent).ToString());
        Assert.That(@event, Is.Not.Null, "Publish not single or not found");
        Assert.Multiple(() =>
        {
            Assert.That(@event.Intent, Is.EqualTo(MessageIntent.Publish.ToString()), "Publish intent mismatch");
            Assert.That(@event.DeliveryDelay, Is.Null, "Event DeliveryDelay");
            Assert.That(@event.DeliveryAt, Is.Null, "Event DeliveryAt");
            Assert.That(@event.Destination, Is.Null, "Event destination");
        });
    }

    class Context : ScenarioContext
    {
        public Guid Id { get; set; }
        public bool WasStarted { get; set; }
        public bool CommandHandled { get; set; }
        public bool EventHandled { get; set; }
        public bool SagaUpdateMessageReceived { get; set; }
        public SagaUpdatedMessage SagaUpdatedMessage { get; set; }
        public DateTimeOffset TimeSent { get; set; }
        public string MessageId { get; set; }
        public bool DelayedByCommandHandled { get; set; }
        public bool DelayAtCommandHandled { get; set; }
    }

    class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() =>
            EndpointSetup<DefaultServer>(config =>
            {
                var receiverEndpoint = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));
                config.AuditSagaStateChanges(receiverEndpoint);
                var routing = config.ConfigureRouting();
                routing.RouteToEndpoint(typeof(TestCommand), AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Endpoint)));
                routing.RouteToEndpoint(typeof(TestDelayAtCommand), AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Endpoint)));
                routing.RouteToEndpoint(typeof(TestDelayedByCommand), AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Endpoint)));
            }, metadata =>
            {
                metadata.RegisterPublisherFor<TestEvent>(typeof(Endpoint));
            });

        public class MySaga(Context testContext) : Saga<MySaga.MySagaData>,
            IAmStartedByMessages<StartSaga>
        {
            public async Task Handle(StartSaga message, IMessageHandlerContext context)
            {
                testContext.WasStarted = true;
                Data.DataId = message.DataId;

                await context.Send(new TestCommand());

                var delayedBySendOptions = new SendOptions();
                delayedBySendOptions.DelayDeliveryWith(TimeSpan.FromSeconds(2));
                await context.Send(new TestDelayedByCommand(), delayedBySendOptions);

                var delayAtSendOptions = new SendOptions();
                delayAtSendOptions.DoNotDeliverBefore(DateTimeOffset.UtcNow.AddSeconds(2));
                await context.Send(new TestDelayAtCommand(), delayAtSendOptions);

                await context.Publish(new TestEvent());

                MarkAsComplete();
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper) => mapper.MapSaga(s => s.DataId).ToMessage<StartSaga>(m => m.DataId);

            public class MySagaData : ContainSagaData
            {
                public virtual Guid DataId { get; set; }
            }
        }

        public class TestCommandHandler(Context testContext) : IHandleMessages<TestCommand>
        {
            public Task Handle(TestCommand message, IMessageHandlerContext context)
            {
                testContext.CommandHandled = true;
                testContext.TimeSent = DateTimeOffsetHelper.ToDateTimeOffset(context.MessageHeaders[Headers.TimeSent]);
                testContext.MessageId = context.MessageId;
                return Task.CompletedTask;
            }
        }

        public class TestDelayedByCommandHandler(Context testContext) : IHandleMessages<TestDelayedByCommand>
        {
            public Task Handle(TestDelayedByCommand message, IMessageHandlerContext context)
            {
                testContext.DelayedByCommandHandled = true;
                return Task.CompletedTask;
            }
        }

        public class TestDelayAtCommandHandler(Context testContext) : IHandleMessages<TestDelayAtCommand>
        {
            public Task Handle(TestDelayAtCommand message, IMessageHandlerContext context)
            {
                testContext.DelayAtCommandHandled = true;
                return Task.CompletedTask;
            }
        }

        public class TestEventHandler(Context testContext) : IHandleMessages<TestEvent>
        {
            public Task Handle(TestEvent message, IMessageHandlerContext context)
            {
                testContext.EventHandled = true;
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
                testContext.SagaUpdateMessageReceived = true;
                testContext.SagaUpdatedMessage = message;
                return Task.CompletedTask;
            }
        }
    }

    public class StartSaga : IMessage
    {
        public Guid DataId { get; set; }
    }

    public class TestCommand : ICommand
    {
    }

    public class TestDelayedByCommand : ICommand
    {
    }

    public class TestDelayAtCommand : ICommand
    {
    }

    public class TestEvent : IEvent
    {
    }
}