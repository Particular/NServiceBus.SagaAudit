namespace NServiceBus.SagaAudit
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;

    class InitializeSagaAuditStateBehavior : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
    {
        public Task Invoke(IIncomingPhysicalMessageContext context, Func<IIncomingPhysicalMessageContext, Task> next)
        {
            context.Extensions.Set(new SagaAuditState());

            return next(context);
        }
    }
}
