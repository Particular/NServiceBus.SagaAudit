namespace ServiceControl.Plugin.Nsb6.SagaAudit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EndpointPlugin.Messages.SagaState;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Saga;
    using NServiceBus.Support;
    using NUnit.Framework;

    public class When_using_complex_saga_properties : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_exclude_them_in_saga_state()
        {
            var contextId = Guid.NewGuid();
            var context = Scenario.Define(new Context(){Id = contextId})
                .WithEndpoint<FakeServiceControl>()
                .WithEndpoint<Sender>(b => b.When(session =>
                {
                    session.SendLocal(new StartSaga
                    {
                        DataId = contextId
                    });
                }))
                .Done(c => c.MessagesReceived.Count == 1)
                .Run();

            var changeMessage = context.MessagesReceived.First(msg => msg?.Initiator?.MessageType == typeof(StartSaga).FullName);
            Assert.AreEqual($"{{\"DataId\":\"{contextId}\",\"Id\":\"{context.SagaId}\",\"Originator\":\"UsingComplexSagaProperties.Sender@{RuntimeEnvironment.MachineName}\",\"OriginalMessageId\":\"{context.OriginalMessageId}\",\"$type\":\"ServiceControl.Plugin.Nsb6.SagaAudit.AcceptanceTests.When_using_complex_saga_properties+Sender+MySaga+MySagaData, NServiceBus.SagaAudit.AcceptanceTests\"}}", changeMessage.SagaState);
        }

        class Context : ScenarioContext
        {
            public Guid Id { get; set; }
            internal List<SagaUpdatedMessage> MessagesReceived { get; } = new List<SagaUpdatedMessage>();
            public Guid SagaId { get; set; }
            public string OriginalMessageId { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    var receiverEndpoint = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));
                    config.AuditSagaStateChanges(Address.Parse(receiverEndpoint));
                });
            }

            public class MySaga : Saga<MySaga.MySagaData>, IAmStartedByMessages<StartSaga>
            {
                public Context TestContext { get; set; }

                public void Handle(StartSaga message)
                {
                    Data.DataId = message.DataId;
                    Data.ComplexNestedObject = new NestedObject
                    {
                        Value = "Some value"
                    };
                    TestContext.OriginalMessageId = Data.OriginalMessageId;
                    TestContext.SagaId = Data.Id;
                }
                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                    mapper.ConfigureMapping<StartSaga>(m => m.DataId).ToSaga(s => s.DataId);
                }

                public class MySagaData : ContainSagaData
                {
                    public virtual Guid DataId { get; set; }
                    public NestedObject ComplexNestedObject { get; set; }
                }

                public class NestedObject
                {
                    public virtual string Value { get; set; }
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
                    TestContext.MessagesReceived.Add(message);
                }
            }
        }

        public class StartSaga : IMessage
        {
            public Guid DataId { get; set; }
        }
    }
}
