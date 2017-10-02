namespace NServiceBus
{
    using System;
    using Configuration.AdvancedExtensibility;

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
        public static void SagaPlugin(this EndpointConfiguration config, string serviceControlQueue)
        {
            config.EnableFeature<SagaAudit.SagaAuditFeature>();
            config.GetSettings().Set("ServiceControl.Queue", serviceControlQueue);
        }
    }
}