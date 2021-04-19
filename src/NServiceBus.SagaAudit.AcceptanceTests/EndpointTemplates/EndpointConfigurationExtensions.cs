namespace NServiceBus.AcceptanceTests
{
    using Configuration.AdvancedExtensibility;

    public static class EndpointConfigurationExtensions
    {
        public static RoutingSettings ConfigureRouting(this EndpointConfiguration configuration) =>
            new RoutingSettings(configuration.GetSettings());
    }
}