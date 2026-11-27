namespace NServiceBus.SagaAudit
{
    using System;

    class SagaAuditState
    {
        public string? InvokedSagas { get; private set; }

        public string? SagaStateChanges { get; private set; }

        public void AddInvokedSaga(string? sagaTypeName, Guid sagaId) => InvokedSagas = Append(InvokedSagas, $"{sagaTypeName}:{sagaId}");

        public void AddSagaStateChange(Guid sagaId, string stateChange) => SagaStateChanges = Append(SagaStateChanges, $"{sagaId}:{stateChange}");

        static string Append(string? current, string entry) => current is null ? entry : $"{current};{entry}";
    }
}
