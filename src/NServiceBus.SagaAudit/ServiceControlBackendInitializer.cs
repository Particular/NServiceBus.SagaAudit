namespace NServiceBus.SagaAudit
{
    using Features;

    class ServiceControlBackendInitializer : FeatureStartupTask
    {
        ServiceControlBackend backend;

        public ServiceControlBackendInitializer(ServiceControlBackend backend)
        {
            this.backend = backend;
        }
        
        protected override void OnStart()
        {
            backend.VerifyIfServiceControlQueueExists();
        }
    }
}