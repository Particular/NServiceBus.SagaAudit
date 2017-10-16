namespace NServiceBus.SagaAudit
{
    using ObjectBuilder;
    using Settings;

    class ServiceControlBackendInitializer : IWantToRunWhenBusStartsAndStops
    {
        ServiceControlBackend backend;

        public ServiceControlBackendInitializer(ReadOnlySettings settings, IBuilder builder)
        {
            if (settings.HasExplicitValue("NServiceBus.SagaAudit.Queue"))
            {
                backend = builder.Build<ServiceControlBackend>();
            }
        }

        public void Start()
        {
            backend?.VerifyIfServiceControlQueueExists();
        }

        public void Stop()
        {
        }
    }
}