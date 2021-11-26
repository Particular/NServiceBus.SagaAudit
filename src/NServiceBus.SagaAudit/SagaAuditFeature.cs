namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
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

            context.Services.AddSingleton(c => new ServiceControlBackend(serviceControlQueue, c.GetRequiredService<ReceiveAddresses>()));
            context.Pipeline.Register(new CaptureSagaStateBehavior.CaptureSagaStateRegistration(context.Settings.EndpointName(), customSagaEntitySerialization));
            context.Pipeline.Register("CaptureSagaResultingMessages", new CaptureSagaResultingMessagesBehavior(), "Reports the messages sent by sagas to ServiceControl");
            context.Pipeline.Register("AuditInvokedSaga", new AuditInvokedSagaBehavior(), "Adds saga information to audit messages");

            context.RegisterStartupTask(b => new SagaAuditStartupTask(b.GetRequiredService<ServiceControlBackend>(), b.GetRequiredService<IMessageDispatcher>()));
        }

        class SagaAuditStartupTask : FeatureStartupTask
        {
            ServiceControlBackend serviceControlBackend;
            IMessageDispatcher dispatcher;

            public SagaAuditStartupTask(ServiceControlBackend backend, IMessageDispatcher dispatcher)
            {
                serviceControlBackend = backend;
                this.dispatcher = dispatcher;
            }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                return serviceControlBackend.Start(dispatcher, cancellationToken);
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0);
            }
        }
    }
}