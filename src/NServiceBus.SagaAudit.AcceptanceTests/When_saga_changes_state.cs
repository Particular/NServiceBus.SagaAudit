namespace ServiceControl.Plugin.Nsb6.SagaAudit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointPlugin.Messages.SagaState;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Features;
    using NUnit.Framework;

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

            //Process Asserts
            Assert.AreNotEqual(Guid.Empty, context.SagaId, "Saga was not started");
            Assert.True(context.TimeoutReceived, "Saga Timeout was not received");
            Assert.NotNull(firstSagaChange, "First Saga Change was not received");
            Assert.NotNull(secondSagaChange, "Second Saga was not received");

            //SagaUpdateMessage Asserts
            Assert.IsNotNull(firstSagaChange.SagaState, "SagaState is not set");
            Assert.IsNotEmpty(firstSagaChange.SagaState, "SagaState is not set");

            Assert.IsTrue(context.MessagesReceived.All(m => m.SagaId != Guid.Empty), "Messages with empty SagaId received");
            Assert.IsTrue(context.MessagesReceived.All(m => m.SagaId == context.SagaId), "Messages with incorrect SagaId received");

            Assert.AreEqual(firstSagaChange.Endpoint, "SagaChangesState.Sender", "Endpoint name is not set or incorrect");
            Assert.True(firstSagaChange.IsNew, "First message is not marked new");
            Assert.False(secondSagaChange.IsNew, "Last message is marked new");
            Assert.False(firstSagaChange.IsCompleted, "First message is marked completed");
            Assert.True(secondSagaChange.IsCompleted, "Last Message is not marked completed");
            Assert.Greater(firstSagaChange.StartTime, DateTime.MinValue, "StartTime is not set");
            Assert.Greater(firstSagaChange.FinishTime, DateTime.MinValue, "FinishTime is not set");
            Assert.AreEqual(firstSagaChange.SagaType, "ServiceControl.Plugin.Nsb6.SagaAudit.AcceptanceTests.When_saga_changes_state+Sender+MySaga", "SagaType is not set or incorrect");

            //SagaUpdateMessage.Initiator Asserts
            Assert.True(secondSagaChange.Initiator.IsSagaTimeoutMessage, "Last message initiator is not a timeout");
            Assert.IsNotNull(firstSagaChange.Initiator,"Initiator has not been set");
            Assert.IsNotNull(firstSagaChange.Initiator.InitiatingMessageId, "Initiator.InitiatingMessageId has not been set");
            Assert.IsNotEmpty(firstSagaChange.Initiator.InitiatingMessageId, "Initiator.InitiatingMessageId has not been set");
            Assert.IsNotNull(firstSagaChange.Initiator.OriginatingMachine, "Initiator.OriginatingMachine has not been set");
            Assert.IsNotEmpty(firstSagaChange.Initiator.OriginatingMachine, "Initiator.OriginatingMachine has not been set");
            Assert.IsNotNull(firstSagaChange.Initiator.OriginatingEndpoint, "Initiator.OriginatingEndpoint has not been set");
            Assert.IsNotEmpty(firstSagaChange.Initiator.OriginatingEndpoint, "Initiator.OriginatingEndpoint has not been set");
            Assert.AreEqual(firstSagaChange.Initiator.MessageType, "ServiceControl.Plugin.Nsb6.SagaAudit.AcceptanceTests.When_saga_changes_state+StartSaga", "First message initiator MessageType is incorrect");
            Assert.IsNotNull(firstSagaChange.Initiator.TimeSent, "Initiator.TimeSent has not been set");

            //SagaUpdateMessages.ResultingMessages Asserts
            Assert.AreEqual(firstSagaChange.ResultingMessages.First().MessageType, "ServiceControl.Plugin.Nsb6.SagaAudit.AcceptanceTests.When_saga_changes_state+Sender+MySaga+TimeHasPassed", "ResultingMessage.MessageType is not set or incorrect");
            Assert.Greater(firstSagaChange.ResultingMessages.First().TimeSent, DateTime.MinValue, "ResultingMessage.TimeSent has not been set");
            //Assert.IsNotNull(firstSagaChange.ResultingMessages.First().DeliveryAt, "ResultingMessage.DeliveryAt has not been set");
            //Assert.IsNotNull(firstSagaChange.ResultingMessages.First().DeliveryDelay, "ResultingMessage.DeliveryDelay has not been set");
            Assert.IsNotNull(firstSagaChange.ResultingMessages.First().Destination, "ResultingMessage.Destination has not been set");
            Assert.IsNotEmpty(firstSagaChange.ResultingMessages.First().Destination, "ResultingMessage.Destination has not been set");
            Assert.IsNotNull(firstSagaChange.ResultingMessages.First().ResultingMessageId, "ResultingMessage.ResultingMessageId has not been not set");
            Assert.IsNotEmpty(firstSagaChange.ResultingMessages.First().ResultingMessageId, "ResultingMessage.ResultingMessageId has not been not set");
            Assert.IsNotNull(firstSagaChange.ResultingMessages.First().Intent,"ResultingMessage.Intent has not been set");
            Assert.IsNotEmpty(firstSagaChange.ResultingMessages.First().Intent,"ResultingMessage.Intent has not been set");
        }

        class Context : ScenarioContext
        {
            public Guid Id { get; set; }

            internal List<SagaUpdatedMessage> MessagesReceived { get; } = new List<SagaUpdatedMessage>();
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
                    var receiverEndpoint = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));
                    config.AuditSagaStateChanges(receiverEndpoint);
                    config.EnableFeature<TimeoutManager>();
                });
            }

            public class MySaga : Saga<MySaga.MySagaData>,
                                        IAmStartedByMessages<StartSaga>,
                                        IHandleTimeouts<MySaga.TimeHasPassed>
            {
                public Context TestContext { get; set; }

                public Task Handle(StartSaga message, IMessageHandlerContext context)
                {
                    TestContext.WasStarted = true;
                    Data.DataId = message.DataId;
                    TestContext.SagaId = Data.Id;
                    Console.WriteLine("Handled");

                    return RequestTimeout(context, TimeSpan.FromMilliseconds(1), new TimeHasPassed());
                }

                public Task Timeout(TimeHasPassed state, IMessageHandlerContext context)
                {
                    MarkAsComplete();

                    TestContext.TimeoutReceived = true;
                    return Task.FromResult(0);
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                    mapper.ConfigureMapping<StartSaga>(m => m.DataId).ToSaga(s => s.DataId);
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
                public Context TestContext { get; set; }

                public Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
                {
                    TestContext.MessagesReceived.Add(message);
                    return Task.FromResult(0);
                }
            }
        }

        public class StartSaga : IMessage
        {
            public Guid DataId { get; set; }
        }
    }
}
