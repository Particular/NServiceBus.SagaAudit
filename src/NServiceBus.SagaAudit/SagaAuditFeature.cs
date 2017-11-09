namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using SagaAudit;
    using Transports;

    class SagaAuditFeature : Feature
    {
        public SagaAuditFeature()
        {
            DependsOn<Sagas>();
            Defaults(s => s.SetDefault(SettingsKeys.CustomSerialization, null));
            RegisterStartupTask<ServiceControlBackendInitializer>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var serviceControlQueue = context.Settings.Get<Address>(SettingsKeys.SagaAuditQueue);
            var customSagaEntitySerialization = context.Settings.GetOrDefault<Func<object, Dictionary<string, string>>>(SettingsKeys.CustomSerialization);

            context.Container.ConfigureComponent(b => new ServiceControlBackend(b.Build<ISendMessages>(), serviceControlQueue, context.Settings.LocalAddress()), DependencyLifecycle.SingleInstance);

            var endpointName = context.Settings.EndpointName();
            context.Container
                .ConfigureProperty<CaptureSagaStateBehavior>(b => b.EndpointName, endpointName)
                .ConfigureProperty<CaptureSagaStateBehavior>(b => b.CustomSagaEntitySerialization, customSagaEntitySerialization);

            context.Pipeline.Register<CaptureSagaStateBehavior.CaptureSagaStateRegistration>();
            context.Pipeline.Register<CaptureSagaResultingMessagesBehavior.CaptureSagaResultingMessageRegistration>();
        }
    }
}