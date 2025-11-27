namespace NServiceBus.SagaAudit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using NServiceBus;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    public class When_saga_changes_state : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_send_result_to_service_control()
        {
            var contextId = Guid.NewGuid();
            var context = await Scenario.Define<Context>(c => { c.Id = contextId; })
                .WithEndpoint<FakeServiceControl>()
                .WithEndpoint<Sender>(b => b.When(session =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.RouteToThisEndpoint();
                    return session.Send(new StartSaga
                    {
                        DataId = contextId
                    }, sendOptions);
                }))
                .Done(c => c.MessagesReceived.Count == 2)
                .Run();

            var firstSagaChange = context.MessagesReceived.FirstOrDefault(msg => msg?.Initiator?.MessageType == typeof(StartSaga).FullName);
            var secondSagaChange = context.MessagesReceived.FirstOrDefault(msg => msg?.Initiator?.IsSagaTimeoutMessage ?? false);

            Assert.Multiple(() =>
            {
                //Process Asserts
                Assert.That(context.SagaId, Is.Not.EqualTo(Guid.Empty), "Saga was not started");
                Assert.That(context.TimeoutReceived, Is.True, "Saga Timeout was not received");
                Assert.That(firstSagaChange, Is.Not.Null, "First Saga Change was not received");
                Assert.That(secondSagaChange, Is.Not.Null, "Second Saga was not received");
            });

            //SagaUpdateMessage Asserts
            Assert.That(firstSagaChange.SagaState, Is.Not.Null, "SagaState is not set");
            Assert.Multiple(() =>
            {
                Assert.That(firstSagaChange.SagaState, Is.Not.Empty, "SagaState is not set");

                Assert.That(context.MessagesReceived.All(m => m.SagaId != Guid.Empty), Is.True, "Messages with empty SagaId received");
                Assert.That(context.MessagesReceived.All(m => m.SagaId == context.SagaId), Is.True, "Messages with incorrect SagaId received");

                Assert.That(firstSagaChange.Endpoint, Is.EqualTo("SagaChangesState.Sender"), "Endpoint name is not set or incorrect");
                Assert.That(firstSagaChange.IsNew, Is.True, "First message is not marked new");
                Assert.That(secondSagaChange.IsNew, Is.False, "Last message is marked new");
                Assert.That(firstSagaChange.IsCompleted, Is.False, "First message is marked completed");
                Assert.That(secondSagaChange.IsCompleted, Is.True, "Last Message is not marked completed");
                Assert.That(firstSagaChange.StartTime, Is.GreaterThan(DateTime.MinValue), "StartTime is not set");
                Assert.That(firstSagaChange.FinishTime, Is.GreaterThan(DateTime.MinValue), "FinishTime is not set");
                Assert.That(firstSagaChange.SagaType, Is.EqualTo("NServiceBus.SagaAudit.AcceptanceTests.When_saga_changes_state+Sender+MySaga"), "SagaType is not set or incorrect");

                //SagaUpdateMessage.Initiator Asserts
                Assert.That(secondSagaChange.Initiator.IsSagaTimeoutMessage, Is.True, "Last message initiator is not a timeout");
                Assert.That(firstSagaChange.Initiator, Is.Not.Null, "Initiator has not been set");
            });
            Assert.Multiple(() =>
            {
                Assert.That(firstSagaChange.Initiator.InitiatingMessageId, Is.Not.Null, "Initiator.InitiatingMessageId has not been set");
                Assert.That(firstSagaChange.Initiator.InitiatingMessageId, Is.Not.Empty, "Initiator.InitiatingMessageId has not been set");
                Assert.That(firstSagaChange.Initiator.OriginatingMachine, Is.Not.Null, "Initiator.OriginatingMachine has not been set");
                Assert.That(firstSagaChange.Initiator.OriginatingMachine, Is.Not.Empty, "Initiator.OriginatingMachine has not been set");
                Assert.That(firstSagaChange.Initiator.OriginatingEndpoint, Is.Not.Null, "Initiator.OriginatingEndpoint has not been set");
                Assert.That(firstSagaChange.Initiator.OriginatingEndpoint, Is.Not.Empty, "Initiator.OriginatingEndpoint has not been set");
                Assert.That(firstSagaChange.Initiator.MessageType, Is.EqualTo("NServiceBus.SagaAudit.AcceptanceTests.When_saga_changes_state+StartSaga"), "First message initiator MessageType is incorrect");
                Assert.That(firstSagaChange.Initiator.TimeSent, Is.Not.Null, "Initiator.TimeSent has not been set");
            });
        }

        class Context : ScenarioContext
        {
            public Guid Id { get; set; }

            internal List<SagaUpdatedMessage> MessagesReceived { get; } = [];
            public bool WasStarted { get; set; }
            public bool TimeoutReceived { get; set; }
            public Guid SagaId { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    var receiverEndpoint = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));
                    config.AuditSagaStateChanges(receiverEndpoint);
                });
            }

            public class MySaga : Saga<MySaga.MySagaData>,
                                        IAmStartedByMessages<StartSaga>,
                                        IHandleTimeouts<MySaga.TimeHasPassed>
            {
                Context testContext;
                public MySaga(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(StartSaga message, IMessageHandlerContext context)
                {
                    testContext.WasStarted = true;
                    Data.DataId = message.DataId;
                    testContext.SagaId = Data.Id;
                    Console.WriteLine("Handled");

                    return RequestTimeout(context, TimeSpan.FromMilliseconds(1), new TimeHasPassed());
                }

                public Task Timeout(TimeHasPassed state, IMessageHandlerContext context)
                {
                    MarkAsComplete();

                    testContext.TimeoutReceived = true;
                    return Task.CompletedTask;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                    mapper.MapSaga(s => s.DataId)
                        .ToMessage<StartSaga>(m => m.DataId);
                }

                public class MySagaData : ContainSagaData
                {
                    public virtual Guid DataId { get; set; }
                }

                public class TimeHasPassed
                {
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
                    testContext.MessagesReceived.Add(message);
                    return Task.CompletedTask;
                }
            }
        }

        public class StartSaga : IMessage
        {
            public Guid DataId { get; set; }
        }
    }
}
