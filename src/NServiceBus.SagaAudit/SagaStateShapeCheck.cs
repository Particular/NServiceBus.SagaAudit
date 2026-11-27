namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;

    static class SagaStateShapeCheck
    {
        public static IEnumerable<string> FindProblems(IEnumerable<Type> sagaDataTypes, JsonSerializerOptions? options)
        {
            foreach (var sagaDataType in sagaDataTypes)
            {
                var typeInfo = TryGetTypeInfo(sagaDataType, options);
                if (typeInfo is null)
                {
                    yield return $"No JSON metadata was found for the saga data type '{sagaDataType.FullName}', so its saga state changes cannot be sent to ServiceControl. Pass JsonSerializerOptions whose TypeInfoResolver includes it to AuditSagaStateChanges.";
                }
                else if (typeInfo.Kind != JsonTypeInfoKind.Object)
                {
                    yield return $"The saga data type '{sagaDataType.FullName}' does not serialize as a JSON object (metadata kind '{typeInfo.Kind}'), for example because a custom converter handles it. ServicePulse only displays saga state that is a JSON object.";
                }
            }
        }

        static JsonTypeInfo? TryGetTypeInfo(Type sagaDataType, JsonSerializerOptions? options)
        {
            var typeInfo = options?.TypeInfoResolver?.GetTypeInfo(sagaDataType, options);
            if (typeInfo is not null || !JsonSerializer.IsReflectionEnabledByDefault)
            {
                return typeInfo;
            }

            try
            {
                return GetTypeInfoWithReflection(sagaDataType, options);
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only called when System.Text.Json reflection serialization is enabled.")]
        static JsonTypeInfo GetTypeInfoWithReflection(Type sagaDataType, JsonSerializerOptions? options)
        {
            options ??= JsonSerializerOptions.Default;
            options.MakeReadOnly(populateMissingResolver: true);
            return options.GetTypeInfo(sagaDataType);
        }
    }
}
