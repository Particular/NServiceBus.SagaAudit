namespace NServiceBus.SagaAudit.AcceptanceTests
{
    using System.Runtime.CompilerServices;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;

    public partial class TestSuiteConstraints : ITestSuiteConstraints
    {
        public bool SupportsDtc => false;
        public bool SupportsCrossQueueTransactions => false;
        public bool SupportsNativePubSub => false;
        public bool SupportsDelayedDelivery => false;
        public bool SupportsOutbox => false;
        public bool SupportsPurgeOnStartup => false;
        public IConfigureEndpointTestExecution CreateTransportConfiguration() => new ConfigureEndpointLearningTransport();
        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointLearningPersistence();

        [ModuleInitializer]
        public static void Initialize() => ITestSuiteConstraints.Current = new TestSuiteConstraints();
    }
}