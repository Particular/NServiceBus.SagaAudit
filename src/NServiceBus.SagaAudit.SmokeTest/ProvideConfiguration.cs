namespace NServiceBus.SagaAudit.SmokeTest
{
    using Config;
    using Config.ConfigurationSource;

    class ProvideConfiguration :
        IProvideConfiguration<MessageForwardingInCaseOfFaultConfig>,
        IProvideConfiguration<AuditConfig>,
        IProvideConfiguration<UnicastBusConfig>
    {
        MessageForwardingInCaseOfFaultConfig IProvideConfiguration<MessageForwardingInCaseOfFaultConfig>.GetConfiguration()
        {
            return new MessageForwardingInCaseOfFaultConfig
            {
                ErrorQueue = "error"
            };
        }

        AuditConfig IProvideConfiguration<AuditConfig>.GetConfiguration()
        {
            return new AuditConfig
            {
                QueueName = "audit"
            };
        }

        UnicastBusConfig IProvideConfiguration<UnicastBusConfig>.GetConfiguration()
        {
            var config = new UnicastBusConfig
            {
                MessageEndpointMappings = new MessageEndpointMappingCollection()
            };

            config.MessageEndpointMappings.Add(new MessageEndpointMapping
            {
                AssemblyName = typeof(Program).Assembly.GetName().Name,
                Endpoint = "NServiceBus.SagaAudit.SmokeTest"
            });
            return config;
        }
    }
}