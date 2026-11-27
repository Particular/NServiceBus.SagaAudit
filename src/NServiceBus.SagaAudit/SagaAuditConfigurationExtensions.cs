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
        /// Sets the ServiceControl queue address and the options used to serialize saga data.
        /// </summary>
        /// <param name="config">The endpoint's configuration to modify</param>
        /// <param name="serviceControlQueue">ServiceControl queue address.</param>
        /// <param name="serializerOptions">The options used to serialize saga data. When trimming or Native AOT is enabled, set a <see cref="JsonSerializerOptions.TypeInfoResolver"/> that provides metadata for every saga data type.</param>
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