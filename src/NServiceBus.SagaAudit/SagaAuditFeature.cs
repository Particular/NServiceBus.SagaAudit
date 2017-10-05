namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using Features;
    using Transport;

    class SagaAuditFeature : Feature
    {
        internal SagaAuditFeature()
        {
            EnableByDefault();
            DependsOn<Sagas>();
        }

        /// <summary>Called when the features is activated.</summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            var serviceControlQueue = context.Settings.Get<string>("NServiceBus.SagaAudit.Queue");
            var customSagaEntitySerialization = context.Settings.GetOrDefault<Func<object, Dictionary<string, string>>>("NServiceBus.SagaAudit.Serialization");

            var backend = new ServiceControlBackend(serviceControlQueue, context.Settings.LocalAddress());

            context.Pipeline.Register(new CaptureSagaStateBehavior.CaptureSagaStateRegistration(context.Settings.EndpointName(), backend, customSagaEntitySerialization));
            context.Pipeline.Register("ReportSagaStateChanges", new CaptureSagaResultingMessagesBehavior(), "Reports the saga state changes to ServiceControl");
            context.Pipeline.Register("AuditInvokedSaga", new AuditInvokedSagaBehavior(), "Adds audit saga information");

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