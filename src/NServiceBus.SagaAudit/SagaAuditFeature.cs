namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using Pipeline;
    using SagaAudit;
    using ServiceControl.Plugin.SagaAudit;

    class SagaAuditFeature : Feature
    {
        public SagaAuditFeature()
        {
            DependsOn<Sagas>();
        }
        
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Settings.TryGet("NServiceBus.SagaAudit.Serialization", out Func<object, Dictionary<string, string>> customSagaEntitySerialization);
            var endpointName = context.Settings.EndpointName();

            var destination = context.Settings.Get<Address>("NServiceBus.SagaAudit.Queue");
            context.Container.ConfigureComponent<ServiceControlBackend>(DependencyLifecycle.SingleInstance)
                .ConfigureProperty(x => x.Destination, destination)
                .ConfigureProperty(x => x.LocalAddress, context.Settings.LocalAddress());

            context.Container
                .ConfigureProperty<CaptureSagaStateBehavior>(x => x.EndpointName, endpointName)
                .ConfigureProperty<CaptureSagaStateBehavior>(x => x.CustomSagaEntitySerialization, customSagaEntitySerialization);

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