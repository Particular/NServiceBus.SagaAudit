namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;

    static class SagaStateShapeCheck
    {
        public static IEnumerable<SagaStateProblem> FindProblems(IEnumerable<Type> sagaDataTypes, JsonSerializerOptions? options)
        {
            // Resolving metadata can make options read-only, and the user's instance might still be configured by other components
            var probeOptions = options is null ? null : new JsonSerializerOptions(options);

            foreach (var sagaDataType in sagaDataTypes)
            {
                var typeInfo = TryGetTypeInfo(sagaDataType, probeOptions);
                if (typeInfo is null)
                {
                    yield return new SagaStateProblem(true, $"No JSON metadata was found for the saga data type '{sagaDataType.FullName}', so its saga state changes cannot be serialized and processing messages for that saga fails. Pass JsonSerializerOptions whose TypeInfoResolver includes it to AuditSagaStateChanges.");
                }
                else if (typeInfo.Kind != JsonTypeInfoKind.Object)
                {
                    yield return new SagaStateProblem(false, $"The saga data type '{sagaDataType.FullName}' does not serialize as a JSON object (metadata kind '{typeInfo.Kind}'), for example because a custom converter handles it. ServicePulse only displays saga state that is a JSON object.");
                }
                else if (FindRenamedStandardProperties(typeInfo) is { Length: > 0 } renamed)
                {
                    yield return new SagaStateProblem(false, $"The saga data type '{sagaDataType.FullName}' writes {string.Join(", ", renamed)}. ServicePulse hides the standard saga data properties only under their declared names, so it shows them as part of the saga state.");
                }
            }
        }

        static string[] FindRenamedStandardProperties(JsonTypeInfo typeInfo)
        {
            var renamed = new List<string>();
            foreach (var property in typeInfo.Properties)
            {
                if (property.AttributeProvider is MemberInfo member && StandardPropertyNames.Contains(member.Name) && property.Name != member.Name)
                {
                    renamed.Add($"'{member.Name}' as '{property.Name}'");
                }
            }

            return [.. renamed];
        }

        static readonly HashSet<string> StandardPropertyNames = [nameof(IContainSagaData.Id), nameof(IContainSagaData.Originator), nameof(IContainSagaData.OriginalMessageId)];

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

    readonly record struct SagaStateProblem(bool IsMissingMetadata, string Message);
}
