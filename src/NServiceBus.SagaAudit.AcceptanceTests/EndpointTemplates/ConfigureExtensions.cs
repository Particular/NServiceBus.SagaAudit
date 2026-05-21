namespace NServiceBus.AcceptanceTests.EndpointTemplates;

using System.Threading.Tasks;
using AcceptanceTesting.Support;

public static class ConfigureExtensions
{
    public static async Task DefineTransport(this EndpointConfiguration config, RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration)
    {
        var transportConfiguration = new ConfigureEndpointLearningTransport();
        await transportConfiguration.Configure(endpointCustomizationConfiguration.EndpointName, config, runDescriptor.Settings, endpointCustomizationConfiguration.PublisherMetadata);
        runDescriptor.OnTestCompleted(_ => transportConfiguration.Cleanup());
    }

    public static async Task DefinePersistence(this EndpointConfiguration config, RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration)
    {
        var persistenceConfiguration = new ConfigureEndpointLearningPersistence();
        await persistenceConfiguration.Configure(endpointCustomizationConfiguration.EndpointName, config, runDescriptor.Settings, endpointCustomizationConfiguration.PublisherMetadata);
        runDescriptor.OnTestCompleted(_ => persistenceConfiguration.Cleanup());
    }
}