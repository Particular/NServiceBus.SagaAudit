namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Configuration.AdvancedExtensibility;
    using Features;
    using SagaAudit;

    /// <summary>
    /// Plugin extension methods.
    /// </summary>
    public static class SagaAuditConfigurationExtensions
    {
        /// <summary>
        /// Sets the ServiceControl queue address.
        /// </summary>
        /// <param name="config">The endpoint's configuration to modify</param>
        /// <param name="serviceControlQueue">ServiceControl queue address.</param>
        /// <param name="customSagaEntitySerialization">A custom strategy for serializing saga state.</param>
        public static void AuditSagaStateChanges(this EndpointConfiguration config, string serviceControlQueue, Func<object, Dictionary<string, string>>? customSagaEntitySerialization = null)
        {
            config.EnableFeature<SagaAuditFeature>();
            config.GetSettings().Set(SettingsKeys.SagaAuditQueue, serviceControlQueue);
            if (customSagaEntitySerialization is not null)
            {
                config.GetSettings().Set(SettingsKeys.CustomSerialization, customSagaEntitySerialization);
            }
        }

        /// <summary>
        /// Sets the ServiceControl queue address and the options used to serialize the saga state that is sent to ServiceControl.
        /// </summary>
        /// <param name="config">The endpoint's configuration to modify</param>
        /// <param name="serviceControlQueue">ServiceControl queue address.</param>
        /// <param name="serializerOptions">The options used to serialize saga data into the saga state that ServiceControl stores and ServicePulse displays.</param>
        /// <remarks>
        /// <para>
        /// These options only affect the saga state sent to ServiceControl. They are independent of how the saga persister stores saga data.
        /// Do not reuse the persister's serializer options, converters, or <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> unless they produce the shape described below.
        /// </para>
        /// <para>
        /// ServicePulse expects each saga data type to serialize as a JSON object whose property names match the property names declared on the type.
        /// Avoid converters that write saga data in another shape and naming policies that rename properties.
        /// The endpoint logs a warning at startup for saga data types that serialize as a JSON array, that a custom converter serializes because their shape cannot be verified, or that write <c>Id</c>, <c>Originator</c>, or <c>OriginalMessageId</c> under another name.
        /// </para>
        /// <para>
        /// When trimming or Native AOT is enabled, set <see cref="JsonSerializerOptions.TypeInfoResolver"/> to provide metadata for every saga data type, for example with a source-generated context that lists them.
        /// When a <see cref="JsonSerializerOptions.TypeInfoResolver"/> is set, it must cover every saga data type; saga data types it omits are not serialized with reflection.
        /// The endpoint fails to start when these options cannot serialize a saga data type, because serializing its saga state would fail every message that saga handles.
        /// </para>
        /// <para>
        /// The endpoint copies these options when it starts, so later changes to them have no effect and the instance passed here stays writable.
        /// </para>
        /// </remarks>
        public static void AuditSagaStateChanges(this EndpointConfiguration config, string serviceControlQueue, JsonSerializerOptions serializerOptions)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceControlQueue);
            ArgumentNullException.ThrowIfNull(serializerOptions);

            config.EnableFeature<SagaAuditFeature>();
            config.GetSettings().Set(SettingsKeys.SagaAuditQueue, serviceControlQueue);
            config.GetSettings().Set(SettingsKeys.SerializerOptions, serializerOptions);
        }
    }
}