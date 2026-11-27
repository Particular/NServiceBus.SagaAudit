namespace NServiceBus.SagaAudit.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AcceptanceTesting;
using NServiceBus;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;
using ServiceControl.EndpointPlugin.Messages.SagaState;

public partial class When_providing_serializer_options : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_serialize_saga_data_with_them()
    {
        var dataId = Guid.NewGuid();
        var context = await Scenario.Define<Context>()
            .WithEndpoint<FakeServiceControl>()
            .WithEndpoint<Sender>(b => b.When(session =>
            {
                var sendOptions = new SendOptions();
                sendOptions.RouteToThisEndpoint();
                return session.Send(new StartSaga { DataId = dataId }, sendOptions);
            }))
            .Done(c => c.MessagesReceived.Count == 1)
            .Run();

        var changeMessage = context.MessagesReceived.First(msg => msg?.Initiator?.MessageType == typeof(StartSaga).FullName);
        using var sagaState = JsonDocument.Parse(changeMessage.SagaState!);

        Assert.That(sagaState.RootElement.GetProperty("dataId").GetGuid(), Is.EqualTo(dataId));
    }

    class Context : ScenarioContext
    {
        internal List<SagaUpdatedMessage> MessagesReceived { get; } = [];
    }

    public class Sender : EndpointConfigurationBuilder
    {
        public Sender() =>
            EndpointSetup<DefaultServer>(config =>
            {
                var receiverEndpoint = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(FakeServiceControl));
                config.AuditSagaStateChanges(receiverEndpoint, SagaDataJsonContext.Default.Options);
            });

        public class MySaga : Saga<MySaga.MySagaData>, IAmStartedByMessages<StartSaga>
        {
            public Task Handle(StartSaga message, IMessageHandlerContext context)
            {
                Data.DataId = message.DataId;
                return Task.CompletedTask;
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper) => mapper.MapSaga(s => s.DataId).ToMessage<StartSaga>(m => m.DataId);

            public class MySagaData : ContainSagaData
            {
                public virtual Guid DataId { get; set; }
            }
        }
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Sender.MySaga.MySagaData))]
    partial class SagaDataJsonContext : JsonSerializerContext;

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
