namespace NServiceBus.SagaAudit
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;

    sealed class SagaStateSerializer
    {
        public SagaStateSerializer(JsonSerializerOptions? configuredOptions) => options = CreateOptions(configuredOptions);

        public string Serialize(object sagaData) => JsonSerializer.Serialize(sagaData, GetTypeInfo(sagaData.GetType()));

        public JsonTypeInfo GetTypeInfo(Type sagaDataType) =>
            options is { IsReadOnly: true } && options.TryGetTypeInfo(sagaDataType, out var typeInfo)
                ? typeInfo
                : throw new InvalidOperationException($"No JSON metadata was found for the saga data type '{sagaDataType.FullName}'. Pass JsonSerializerOptions whose TypeInfoResolver includes it to AuditSagaStateChanges.");

        static JsonSerializerOptions? CreateOptions(JsonSerializerOptions? configuredOptions)
        {
            if (configuredOptions is null)
            {
                return JsonSerializer.IsReflectionEnabledByDefault ? GetDefaultOptions() : null;
            }

            // Options only cache metadata once read-only; locking a copy keeps the caller's instance writable
            var options = new JsonSerializerOptions(configuredOptions);
            if (options.TypeInfoResolver is not null)
            {
                options.MakeReadOnly();
            }
            else if (JsonSerializer.IsReflectionEnabledByDefault)
            {
                MakeReadOnlyWithReflectionResolver(options);
            }

            return options;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
        static JsonSerializerOptions GetDefaultOptions() => JsonSerializerOptions.Default;

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
        static void MakeReadOnlyWithReflectionResolver(JsonSerializerOptions options) => options.MakeReadOnly(populateMissingResolver: true);

        readonly JsonSerializerOptions? options;
    }
}
