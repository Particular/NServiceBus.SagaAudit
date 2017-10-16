namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using Configuration.AdvanceExtensibility;
    using Features;

    /// <summary>
    /// Plugin extension methods.
    /// </summary>
    public static class SagaAuditConfigurationExtensions
    {
        /// <summary>
        /// Sets the ServiceControl queue address.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="serviceControlQueue">ServiceControl queue address.</param>
        /// <param name="customSagaEntitySerialization">A custom strategy for serializing saga state.</param>
        public static void AuditSagaStateChanges(this BusConfiguration config, Address serviceControlQueue, Func<object, Dictionary<string, string>> customSagaEntitySerialization = null)
        {
            config.EnableFeature<SagaAuditFeature>();
            config.GetSettings().Set("NServiceBus.SagaAudit.Queue", serviceControlQueue);
            if (customSagaEntitySerialization != null)
            {
                config.GetSettings().Set("NServiceBus.SagaAudit.Serialization", customSagaEntitySerialization);
            }
        }
    }
}