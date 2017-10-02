namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using Pipeline;
    using Sagas;

    class AuditInvokedSagaBehavior : Behavior<IInvokeHandlerContext>
    {
        public override async Task Invoke(IInvokeHandlerContext context, Func<Task> next)
        {
            await next().ConfigureAwait(false);

            ActiveSagaInstance activeSagaInstance;

            if (!context.Extensions.TryGet(out activeSagaInstance))
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

            string invokedSagasHeader;

            if (headers.TryGetValue(SagaAuditHeaders.InvokedSagas, out invokedSagasHeader))
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
