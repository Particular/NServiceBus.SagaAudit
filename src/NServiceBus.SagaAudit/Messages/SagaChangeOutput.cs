namespace ServiceControl.EndpointPlugin.Messages.SagaState
{
    using System;
    using System.Runtime.Serialization;

    [DataContract]
    class SagaChangeOutput
    {
        [DataMember]
        public string MessageType { get; set; }
        [DataMember]
        public DateTime TimeSent { get; set; }
        [DataMember]
        public DateTime? DeliveryAt { get; set; }
        [DataMember]
        public TimeSpan? DeliveryDelay { get; set; }
        [DataMember]
        public string Destination { get; set; }
        [DataMember]
        public string ResultingMessageId { get; set; }
        [DataMember]
        public string Intent { get; set; }
    }
}