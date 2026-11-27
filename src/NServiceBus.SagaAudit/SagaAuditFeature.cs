namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
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
            var customSagaEntitySerialization = context.Settings.GetOrDefault<Func<object, Dictionary<string, string>>?>(SettingsKeys.CustomSerialization);
            var serializerOptions = context.Settings.GetOrDefault<JsonSerializerOptions?>(SettingsKeys.SerializerOptions);

            if (customSagaEntitySerialization is null)
            {
                var sagaDataTypes = context.Settings.Get<NServiceBus.Sagas.SagaMetadataCollection>().Select(saga => saga.SagaEntityType);
                foreach (var problem in SagaStateShapeCheck.FindProblems(sagaDataTypes, serializerOptions))
                {
                    Logger.Warn(problem);
                }
            }

            context.Services.AddSingleton(c => new ServiceControlBackend(serviceControlQueue, c.GetRequiredService<ReceiveAddresses>()));
            context.Pipeline.Register("InitializeSagaAuditState", new InitializeSagaAuditStateBehavior(), "Creates the per-message state that carries saga information to the audit message");
            context.Pipeline.Register(new CaptureSagaStateBehavior.CaptureSagaStateRegistration(context.Settings.EndpointName(), customSagaEntitySerialization, serializerOptions));
            context.Pipeline.Register("CaptureSagaResultingMessages", new CaptureSagaResultingMessagesBehavior(), "Reports the messages sent by sagas to ServiceControl");
            context.Pipeline.Register("AuditInvokedSaga", new AuditSagaMetadataBehavior(), "Adds saga information to audit messages");

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
                return Task.CompletedTask;
            }
        }

        static readonly ILog Logger = LogManager.GetLogger<SagaAuditFeature>();
    }
}