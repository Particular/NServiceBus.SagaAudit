namespace NServiceBus.SagaAudit.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Features;
    using NUnit.Framework;
    using Saga;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    class When_a_saga_results_in_messages
    {
        [Test]
        public void Those_messages_should_be_captured()
        {
            var contextId = Guid.NewGuid();
            var context = Scenario.Define(new Context()
                {
                    Id = contextId
                })
                .WithEndpoint<FakeServiceControl>()
                .WithEndpoint<Endpoint>(b => b.When(session =>
                {
                    session.SendLocal(new StartSaga
                    {
                        DataId = contextId
                    });
                }))
                .Done(c => c.WasStarted && c.CommandHandled && c.DelayedByCommandHandled && c.DelayAtCommandHandled && c.EventHandled && c.SagaUpdateMessageReceived)
                .Run(TimeSpan.FromSeconds(120));

            var sagaupdate = context.SagaUpdatedMessage;

            var command = sagaupdate.ResultingMessages.SingleOrDefault(m => m.MessageType == typeof(TestCommand).ToString());
            Assert.IsNotNull(command, "Command messages not single or not found");
            Assert.AreEqual(MessageIntentEnum.Send.ToString(), command.Intent, "Command intent mismatch");
            Assert.AreEqual(context.MessageId, command.ResultingMessageId, "MessageId mismatch");
            Assert.AreEqual(context.TimeSent, command.TimeSent, "TimeSent mismatch");
            Assert.IsTrue(Regex.IsMatch(command.Destination, $"{AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Endpoint))}@.*"), "Destination mismatch");
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
            Assert.AreEqual(MessageIntentEnum.Publish.ToString(), @event.Intent, "Publish intent mismatch");
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
            public DateTime TimeSent { get; set; }
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
                    config.EnableFeature<TimeoutManager>();
                    config.AuditSagaStateChanges(receiverEndpoint);
                })
                .AddMapping<TestEvent>(typeof(Endpoint))
                .AddMapping<TestCommand>(typeof(Endpoint));
            }

            public class MySaga : Saga<MySaga.MySagaData>,
                                        IAmStartedByMessages<StartSaga>
            {
                public Context TestContext { get; set; }

                public void Handle(StartSaga message)
                {
                    TestContext.WasStarted = true;
                    Data.DataId = message.DataId;

                    Bus.Send(new TestCommand());
                    Bus.Defer(TimeSpan.FromSeconds(2), new TestDelayedByCommand());
                    Bus.Defer(DateTime.UtcNow.AddSeconds(2), new TestDelayAtCommand());
                    Bus.Publish(new TestEvent());

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
                public Context TestContext { get; set; }
                public IBus Bus { get; set; }
                public void Handle(TestCommand message)
                {
                    TestContext.CommandHandled = true;
                    TestContext.TimeSent = DateTimeExtensions.ToUtcDateTime(Bus.CurrentMessageContext.Headers[Headers.TimeSent]);
                    TestContext.MessageId = Bus.CurrentMessageContext.Id;
                }
            }

            public class TestDelayedByCommandHandler : IHandleMessages<TestDelayedByCommand>
            {
                public Context TestContext { get; set; }
                public void Handle(TestDelayedByCommand message)
                {
                    TestContext.DelayedByCommandHandled = true;
                }
            }

            public class TestDelayAtCommandHandler : IHandleMessages<TestDelayAtCommand>
            {
                public Context TestContext { get; set; }
                public void Handle(TestDelayAtCommand message)
                {
                    TestContext.DelayAtCommandHandled = true;
                }
            }

            public class TestEventHandler : IHandleMessages<TestEvent>
            {
                public Context TestContext { get; set; }

                public void Handle(TestEvent message)
                {
                    TestContext.EventHandled = true;
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
                public Context TestContext { get; set; }

                public void Handle(SagaUpdatedMessage message)
                {
                    TestContext.SagaUpdateMessageReceived = true;
                    TestContext.SagaUpdatedMessage = message;
                }
            }
        }

        public class StartSaga : IMessage
        {
            public Guid DataId { get; set; }
        }

        public class TestCommand : ICommand
        { }

        public class TestDelayedByCommand : ICommand
        { }

        public class TestDelayAtCommand : ICommand
        { }

        public class TestEvent : IEvent
        { }
    }
}
