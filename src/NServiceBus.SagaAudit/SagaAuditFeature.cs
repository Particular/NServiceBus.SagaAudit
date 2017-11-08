namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using SagaAudit;
    using Transport;

    class SagaAuditFeature : Feature
    {
        public SagaAuditFeature()
        {
            DependsOn<Sagas>();
            Defaults(s => s.SetDefault(SettingsKeys.CustomSerialization, null));
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var serviceControlQueue = context.Settings.Get<string>(SettingsKeys.SagaAuditQueue);
            var customSagaEntitySerialization = context.Settings.GetOrDefault<Func<object, Dictionary<string, string>>>(SettingsKeys.CustomSerialization);

            var backend = new ServiceControlBackend(serviceControlQueue, context.Settings.LocalAddress());
            context.Pipeline.Register(new CaptureSagaStateBehavior.CaptureSagaStateRegistration(context.Settings.EndpointName(), backend, customSagaEntitySerialization));
            context.Pipeline.Register("CaptureSagaResultingMessages", new CaptureSagaResultingMessagesBehavior(), "Reports the messages sent by sagas to ServiceControl");
            context.Pipeline.Register("AuditInvokedSaga", new AuditInvokedSagaBehavior(), "Adds saga information to audit messages");

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
    }
}