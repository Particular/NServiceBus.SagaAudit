namespace ServiceControl.EndpointPlugin.Messages.SagaState
{
    using System;
    using System.Runtime.Serialization;

    [DataContract]
    class SagaChangeInitiator
    {
        [DataMember]
        public string InitiatingMessageId { get; set; }
        [DataMember]
        public string MessageType { get; set; }
        [DataMember]
        public bool IsSagaTimeoutMessage { get; set; }
        [DataMember]
        public DateTime? TimeSent { get; set; }
        [DataMember]
        public string OriginatingMachine { get; set; }
        [DataMember]
        public string OriginatingEndpoint { get; set; }
        [DataMember]
        public string Intent { get; set; }
    }

}