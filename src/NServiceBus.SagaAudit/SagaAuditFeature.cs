namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using Pipeline;
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
            context.Settings.TryGet(SettingsKeys.CustomSerialization, out Func<object, Dictionary<string, string>> customSagaEntitySerialization);
            var endpointName = context.Settings.EndpointName();

            var destination = context.Settings.Get<Address>(SettingsKeys.SagaAuditQueue);
            var localAddress = context.Settings.LocalAddress();
            context.Container.ConfigureComponent(b => new ServiceControlBackend(b.Build<ISendMessages>(), destination, localAddress), DependencyLifecycle.SingleInstance);

            context.Container.ConfigureComponent(b => new CaptureSagaStateBehavior(b.Build<ServiceControlBackend>(), endpointName, customSagaEntitySerialization), DependencyLifecycle.SingleInstance);

            context.Pipeline.Register<CaptureSagaStateRegistration>();
            context.Pipeline.Register<CaptureSagaResultingMessageRegistration>();
        }

        class CaptureSagaStateRegistration : RegisterStep
        {
            public CaptureSagaStateRegistration()
                : base("CaptureSagaState", typeof(CaptureSagaStateBehavior), "Records saga state changes")
            {
                InsertBefore(WellKnownStep.InvokeSaga);
            }
        }

        class CaptureSagaResultingMessageRegistration : RegisterStep
        {
            public CaptureSagaResultingMessageRegistration()
                : base("ReportSagaStateChanges", typeof(CaptureSagaResultingMessagesBehavior), "Reports the saga state changes to ServiceControl")
            {
                InsertBefore(WellKnownStep.DispatchMessageToTransport);
            }
        }
    }
}