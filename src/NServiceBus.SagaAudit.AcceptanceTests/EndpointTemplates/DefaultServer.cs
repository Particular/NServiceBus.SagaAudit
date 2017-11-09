namespace NServiceBus.SagaAudit.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using Config.ConfigurationSource;
    using Configuration.AdvanceExtensibility;
    using Hosting.Helpers;
    using Logging;
    using NServiceBus;

    public class DefaultServer : IEndpointSetupTemplate
    {
        readonly List<Type> typesToInclude;

        public DefaultServer()
        {
            typesToInclude = new List<Type>();
        }

        public DefaultServer(List<Type> typesToInclude)
        {
            this.typesToInclude = typesToInclude;
        }

        public BusConfiguration GetConfiguration(RunDescriptor runDescriptor, EndpointConfiguration endpointConfiguration, IConfigurationSource configSource, Action<BusConfiguration> configurationBuilderCustomization)
        {
            LogManager.UseFactory(new ContextAppender(runDescriptor.ScenarioContext, endpointConfiguration.EndpointName));

            var types = GetTypesScopedByTestClass(endpointConfiguration);

            typesToInclude.AddRange(types);

            var builder = new BusConfiguration();

            builder.EndpointName(endpointConfiguration.EndpointName);
            builder.TypesToScan(typesToInclude);
            builder.CustomConfigurationSource(configSource);
            builder.EnableInstallers();
            builder.UseTransport<MsmqTransport>();
            builder.RegisterComponents(r =>
            {
                r.RegisterSingleton(runDescriptor.ScenarioContext.GetType(), runDescriptor.ScenarioContext);
                r.RegisterSingleton(typeof(ScenarioContext), runDescriptor.ScenarioContext);
            });

            builder.UseSerialization<JsonSerializer>();
            builder.UsePersistence<InMemoryPersistence>();

            builder.GetSettings().SetDefault("ScaleOut.UseSingleBrokerQueue", true);
            configurationBuilderCustomization(builder);


            return builder;
        }

        static IEnumerable<Type> GetTypesScopedByTestClass(EndpointConfiguration endpointConfiguration)
        {
            var scanner = new AssemblyScanner
            {
                ThrowExceptions = false
            };

            var assemblies = scanner.GetScannableAssemblies();

            var types = assemblies.Assemblies
                //exclude all test types by default
                                  .Where(a => a != Assembly.GetExecutingAssembly())
                                  .SelectMany(a => a.GetTypes());


            var scopedTypes = GetNestedTypeRecursive(endpointConfiguration.BuilderType.DeclaringType, endpointConfiguration.BuilderType);
            types = types.Union(scopedTypes);
            types = types.Union(endpointConfiguration.TypesToInclude);

            return types.Where(t => !endpointConfiguration.TypesToExclude.Contains(t)).ToList();
        }

        static IEnumerable<Type> GetNestedTypeRecursive(Type rootType, Type builderType)
        {
            yield return rootType;

            if (typeof(IEndpointConfigurationFactory).IsAssignableFrom(rootType) && rootType != builderType)
                yield break;

            foreach (var nestedType in rootType.GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SelectMany(t => GetNestedTypeRecursive(t, builderType)))
            {
                yield return nestedType;
            }
        }

    }
}