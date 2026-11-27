namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using NUnit.Framework;

    [TestFixture]
    public partial class SagaStateSerializerOptions
    {
        [Test]
        public void Should_use_type_info_from_the_serializer_options()
        {
            var serialized = Serialize(new SagaData { DataId = DataId }, serializerOptions: SagaDataJsonContext.Default.Options);

            Assert.That(serialized, Is.EqualTo(JsonSerializer.Serialize(new SagaData { DataId = DataId }, SagaDataJsonContext.Default.SagaData)));
            Assert.That(serialized, Does.Contain($"\"dataId\":\"{DataId}\""));
        }

        [Test]
        public void Should_fall_back_to_reflection_with_the_serializer_options_when_they_have_no_type_info()
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            var serialized = Serialize(new SagaData { DataId = DataId }, serializerOptions: options);

            Assert.That(serialized, Does.Contain($"\"dataId\":\"{DataId}\""));
        }

        [Test]
        public void Should_serialize_with_default_options_when_none_are_configured()
        {
            var serialized = Serialize(new SagaData { DataId = DataId });

            Assert.That(serialized, Is.EqualTo(JsonSerializer.Serialize(new SagaData { DataId = DataId })));
        }

        [Test]
        public void Should_serialize_the_custom_serialization_result_like_reflection_does()
        {
            var custom = new Dictionary<string, string> { ["MyPropertyName"] = "value with \"quotes\" and <html> & ünicode" };

            var serialized = Serialize(new SagaData { DataId = DataId }, customSagaEntitySerialization: _ => custom);

            Assert.That(serialized, Is.EqualTo(JsonSerializer.Serialize(custom)));
        }

        static string Serialize(SagaData sagaData, Func<object, Dictionary<string, string>>? customSagaEntitySerialization = null, JsonSerializerOptions? serializerOptions = null)
            => new CaptureSagaStateBehavior("FakeEndpointName", null!, customSagaEntitySerialization, new SagaStateSerializer(serializerOptions)).SerializeSagaState(sagaData);

        static readonly Guid DataId = Guid.Parse("6a3c3a5e-55f0-4c9b-9f3c-2f2f0a7f9b11");

        public class SagaData : ContainSagaData
        {
            public Guid DataId { get; set; }
        }

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        [JsonSerializable(typeof(SagaData))]
        partial class SagaDataJsonContext : JsonSerializerContext;
    }
}
