namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Pipeline;
    using Sagas;

    class AuditInvokedSagaBehavior : Behavior<IInvokeHandlerContext>
    {
        public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
        {
            await next().ConfigureAwait(false);

            if (!context.Extensions.TryGet<ActiveSagaInstance>(out var activeSagaInstance))
            {
                return;
            }

            AddOrUpdateInvokedSagasHeader(context.Headers, activeSagaInstance.Instance);
        }

        public static void AddOrUpdateInvokedSagasHeader(Dictionary<string, string> headers, Saga sagaInstance)
        {
            if (sagaInstance.Entity == null)
            {
                return;
            }

            var invokedSagaAuditData = $"{sagaInstance.GetType().FullName}:{sagaInstance.Entity.Id}";

            if (headers.TryGetValue(SagaAuditHeaders.InvokedSagas, out var invokedSagasHeader))
            {
                headers[SagaAuditHeaders.InvokedSagas] = $"{invokedSagasHeader};{invokedSagaAuditData}";
            }
            else
            {
                headers.Add(SagaAuditHeaders.InvokedSagas, invokedSagaAuditData);
            }
        }
    }
}
