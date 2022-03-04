namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;
    using SagaAudit;

    public class When_a_message_triggers_multiple_saga_invocations
    {
        [Test]
        public void It_should_store_all_ids_in_the_header()
        {
            var headers = new Dictionary<string, string>();

            AuditInvokedSagaBehavior.AddOrUpdateInvokedSagasHeader(headers, new MySaga());
            AuditInvokedSagaBehavior.AddOrUpdateInvokedSagasHeader(headers, new MySaga());

            var invokedSagas = headers["NServiceBus.InvokedSagas"];

            Assert.AreEqual("NServiceBus.SagaAudit.Tests.When_a_message_triggers_multiple_saga_invocations+MySaga:00000000-0000-0000-0000-000000000000;NServiceBus.SagaAudit.Tests.When_a_message_triggers_multiple_saga_invocations+MySaga:00000000-0000-0000-0000-000000000000", invokedSagas);
        }

        class MySaga : Saga<Data>, IAmStartedByMessages<SagaStartMessage>
        {
            public MySaga()
            {
                Entity = new Data();
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<Data> mapper) =>
                mapper.MapSaga(s => s.SagaId).ToMessage<SagaStartMessage>(m => m.Id);

            public Task Handle(SagaStartMessage message, IMessageHandlerContext context) => Task.CompletedTask;
        }
        class Data : ContainSagaData
        {
            public Guid SagaId { get; set; }
        }

        class SagaStartMessage : IMessage
        {
            public Guid Id { get; set; }
        }
    }
}
