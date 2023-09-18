namespace NServiceBus.SagaAudit.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus.AcceptanceTests;
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
                .WithEndpoint<Endpoint>(b => b.When(session => session.SendLocal(new StartSaga
                {
                    DataId = contextId
                })))
                .Done(c => c.WasStarted && c.CommandHandled && c.DelayedByCommandHandled && c.DelayAtCommandHandled && c.EventHandled && c.SagaUpdateMessageReceived)
                .Run();

            var sagaupdate = context.SagaUpdatedMessage;

            var command = sagaupdate.ResultingMessages.SingleOrDefault(m => m.MessageType == typeof(TestCommand).ToString());
            Assert.IsNotNull(command, "Command messages not single or not found");
            Assert.AreEqual(MessageIntent.Send.ToString(), command.Intent, "Command intent mismatch");
            Assert.AreEqual(context.MessageId, command.ResultingMessageId, "MessageId mismatch");
            Assert.Less(Math.Abs((context.TimeSent - command.TimeSent).TotalSeconds), 1d, "TimeSent mismatch"); //Test within 1 second rounded, since now we have to populate TimeSent with UtcNow as the header is not yet set
            Assert.AreEqual(AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Endpoint)), command.Destination, "Destination mismatch");
            Assert.IsNull(command.DeliveryDelay, "Command DeliveryDelay");
            Assert.IsNull(command.DeliveryAt, "Command DeliveryAt");

            var delayedByCommand = sagaupdate.ResultingMessages.SingleOrDefault(m => m.MessageType == typeof(TestDelayedByCommand).ToString());
            Assert.IsNotNull(delayedByCommand, "Delayed by command message not single or not found");
            Assert.IsNotNull(delayedByCommand.DeliveryDelay, "Delay by Command");

            var delayedAtCommand = sagaupdate.ResultingMessages.SingleOrDefault(m => m.MessageType == typeof(TestDelayAtCommand).ToString());
            Assert.IsNotNull(delayedAtCommand, "Delayed at command message not single or not found");
            Assert.IsNotNull(delayedAtCommand.DeliveryAt, "Delivery At Command");

            var @event = sagaupdate.ResultingMessages.SingleOrDefault(m => m.MessageType == typeof(TestEvent).ToString());
            Assert.IsNotNull(@event, "Publish not single or not found");
            Assert.AreEqual(MessageIntent.Publish.ToString(), @event.Intent, "Publish intent mismatch");
            Assert.IsNull(@event.DeliveryDelay, "Event DeliveryDelay");
            Assert.IsNull(@event.DeliveryAt, "Event DeliveryAt");
            Assert.IsNull(@event.Destination, "Event destination");
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
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    var receiverEndpoint = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));
                    config.AuditSagaStateChanges(receiverEndpoint);
                    var routing = EndpointConfigurationExtensions.ConfigureRouting(config);
                    routing.RouteToEndpoint(typeof(TestCommand), AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Endpoint)));
                    routing.RouteToEndpoint(typeof(TestDelayAtCommand), AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Endpoint)));
                    routing.RouteToEndpoint(typeof(TestDelayedByCommand), AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Endpoint)));
                }, metadata =>
                {
                    metadata.RegisterPublisherFor<TestEvent>(typeof(Endpoint));
                });
            }

            public class MySaga : Saga<MySaga.MySagaData>,
                IAmStartedByMessages<StartSaga>
            {
                Context testContext;
                public MySaga(Context testContext)
                {
                    this.testContext = testContext;
                }

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

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                    mapper.ConfigureMapping<StartSaga>(m => m.DataId).ToSaga(s => s.DataId);
                }

                public class MySagaData : ContainSagaData
                {
                    public virtual Guid DataId { get; set; }
                }
            }

            public class TestCommandHandler : IHandleMessages<TestCommand>
            {
                Context testContext;
                public TestCommandHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(TestCommand message, IMessageHandlerContext context)
                {
                    testContext.CommandHandled = true;
                    testContext.TimeSent = DateTimeOffsetHelper.ToDateTimeOffset(context.MessageHeaders[Headers.TimeSent]);
                    testContext.MessageId = context.MessageId;
                    return Task.FromResult(0);
                }
            }

            public class TestDelayedByCommandHandler : IHandleMessages<TestDelayedByCommand>
            {
                Context testContext;
                public TestDelayedByCommandHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(TestDelayedByCommand message, IMessageHandlerContext context)
                {
                    testContext.DelayedByCommandHandled = true;
                    return Task.FromResult(0);
                }
            }

            public class TestDelayAtCommandHandler : IHandleMessages<TestDelayAtCommand>
            {
                Context testContext;
                public TestDelayAtCommandHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(TestDelayAtCommand message, IMessageHandlerContext context)
                {
                    testContext.DelayAtCommandHandled = true;
                    return Task.FromResult(0);
                }
            }

            public class TestEventHandler : IHandleMessages<TestEvent>
            {
                Context testContext;
                public TestEventHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(TestEvent message, IMessageHandlerContext context)
                {
                    testContext.EventHandled = true;
                    return Task.FromResult(0);
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

            public class SagaUpdatedMessageHandler : IHandleMessages<SagaUpdatedMessage>
            {
                Context testContext;
                public SagaUpdatedMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
                {
                    testContext.SagaUpdateMessageReceived = true;
                    testContext.SagaUpdatedMessage = message;
                    return Task.FromResult(0);
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
}