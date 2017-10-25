namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using Pipeline;
    using SagaAudit;
    using Transport;
    using Transports;

    class SagaAuditFeature : Feature
    {
        public SagaAuditFeature()
        {
            DependsOn<Sagas>();
            Defaults(s => s.SetDefault(SettingsKeys.CustomSerialization, null));
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var serviceControlQueue = context.Settings.Get<string>("NServiceBus.SagaAudit.Queue");
            var customSagaEntitySerialization = context.Settings.GetOrDefault<Func<object, Dictionary<string, string>>>(SettingsKeys.CustomSerialization);

            var backend = new ServiceControlBackend(serviceControlQueue, context.Settings.LocalAddress());

            context.Container.ConfigureComponent(b => new CaptureSagaStateBehavior(b.Build<ServiceControlBackend>(), context.Settings.EndpointName(), customSagaEntitySerialization), DependencyLifecycle.SingleInstance);

            context.Pipeline.Register<CaptureSagaStateRegistration>();
            context.Pipeline.Register<CaptureSagaResultingMessageRegistration>();
            context.RegisterStartupTask(b => new SagaAuditStartupTask(backend, b.Build<IDispatchMessages>()));
        }

        class SagaAuditStartupTask : FeatureStartupTask
        {
            ServiceControlBackend serviceControlBackend;
            IDispatchMessages dispatcher;

            public SagaAuditStartupTask(ServiceControlBackend backend, IDispatchMessages dispatcher)
            {
                serviceControlBackend = backend;
                this.dispatcher = dispatcher;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return serviceControlBackend.Start(dispatcher);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.FromResult(0);
            }
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