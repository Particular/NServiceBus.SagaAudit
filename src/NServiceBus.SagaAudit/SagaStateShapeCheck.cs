namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text.Json.Serialization.Metadata;

    static class SagaStateShapeCheck
    {
        public static IEnumerable<SagaStateProblem> FindProblems(IEnumerable<Type> sagaDataTypes, SagaStateSerializer sagaStateSerializer)
        {
            foreach (var sagaDataType in sagaDataTypes)
            {
                JsonTypeInfo? typeInfo = null;
                Exception? resolutionFailure = null;
                try
                {
                    typeInfo = sagaStateSerializer.GetTypeInfo(sagaDataType);
                }
                catch (Exception exception)
                {
                    resolutionFailure = exception;
                }

                if (typeInfo is null)
                {
                    yield return new SagaStateProblem(true, $"The saga state of the saga data type '{sagaDataType.FullName}' cannot be serialized, so processing messages for that saga fails. {resolutionFailure?.Message}");
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
    }

    readonly record struct SagaStateProblem(bool PreventsSerialization, string Message);
}
