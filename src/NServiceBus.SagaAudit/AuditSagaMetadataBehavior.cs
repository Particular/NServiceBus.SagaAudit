namespace NServiceBus.SagaAudit
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;

    class AuditSagaMetadataBehavior : IBehavior<IAuditContext, IAuditContext>
    {
        public Task Invoke(IAuditContext context, Func<IAuditContext, Task> next)
        {
            if (context.Extensions.TryGet<SagaAuditState>(out var state))
            {
                if (state.InvokedSagas is not null)
                {
                    context.AuditMetadata[SagaAuditHeaders.InvokedSagas] = state.InvokedSagas;
                }

                if (state.SagaStateChanges is not null)
                {
                    context.AuditMetadata[SagaAuditHeaders.SagaStateChange] = state.SagaStateChanges;
                }
            }

            return next(context);
        }
    }
}
