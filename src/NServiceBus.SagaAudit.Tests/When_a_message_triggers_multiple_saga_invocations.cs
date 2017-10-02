namespace ServiceControl.Plugin.Nsb6.SagaAudit.Tests
{
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.SagaAudit;
    using NUnit.Framework;

    public class When_a_message_triggers_multiple_saga_invocations
    {
        [Test]
        public void It_should_store_all_ids_in_the_header()
        {
            var headers = new Dictionary<string, string>();

            AuditInvokedSagaBehavior.AddOrUpdateInvokedSagasHeader(headers, new MySaga());
            AuditInvokedSagaBehavior.AddOrUpdateInvokedSagasHeader(headers, new MySaga());

            var invokedSagas = headers["NServiceBus.InvokedSagas"];

            Assert.AreEqual("ServiceControl.Plugin.Nsb6.SagaAudit.Tests.When_a_message_triggers_multiple_saga_invocations+MySaga:00000000-0000-0000-0000-000000000000;ServiceControl.Plugin.Nsb6.SagaAudit.Tests.When_a_message_triggers_multiple_saga_invocations+MySaga:00000000-0000-0000-0000-000000000000", invokedSagas);
        }

        class MySaga : Saga
        {
            public MySaga()
            {
                Entity = new Data();
            }

            protected override void ConfigureHowToFindSaga(IConfigureHowToFindSagaWithMessage sagaMessageFindingConfiguration)
            {
            }

            class Data : ContainSagaData
            {
            }
        }
    }
}
