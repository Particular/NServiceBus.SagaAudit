namespace NServiceBus.SagaAudit
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using ServiceControl.EndpointPlugin.Messages.SagaState;

    [JsonSerializable(typeof(SagaUpdatedMessage))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    partial class SagaAuditJsonContext : JsonSerializerContext;
}
