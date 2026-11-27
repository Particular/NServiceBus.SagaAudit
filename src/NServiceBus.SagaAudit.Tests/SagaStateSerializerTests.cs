namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using NUnit.Framework;

    [TestFixture]
    public partial class SagaStateSerializerTests
    {
        [Test]
        public void Should_reuse_metadata_across_saga_updates_without_configured_options() => AssertMetadataIsReused(new SagaStateSerializer(null));

        [Test]
        public void Should_reuse_metadata_across_saga_updates_with_a_configured_resolver() =>
            AssertMetadataIsReused(new SagaStateSerializer(new JsonSerializerOptions { TypeInfoResolver = SerializerTestsJsonContext.Default }));

        [Test]
        public void Should_reuse_metadata_across_saga_updates_with_configured_options_without_a_resolver() =>
            AssertMetadataIsReused(new SagaStateSerializer(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        [Test]
        public void Should_not_fall_back_to_reflection_when_the_configured_resolver_omits_the_saga_data_type()
        {
            var serializer = new SagaStateSerializer(UnrelatedJsonContext.Default.Options);

            var exception = Assert.Throws<InvalidOperationException>(() => serializer.Serialize(new SagaData()));

            Assert.That(exception!.Message, Does.Contain("No JSON metadata was found").And.Contain(typeof(SagaData).FullName));
        }

        [Test]
        public void Should_leave_the_configured_options_writable()
        {
            var options = new JsonSerializerOptions { TypeInfoResolver = SerializerTestsJsonContext.Default };

            _ = new SagaStateSerializer(options).Serialize(new SagaData());

            Assert.That(options.IsReadOnly, Is.False);
        }

        static void AssertMetadataIsReused(SagaStateSerializer serializer)
        {
            var first = serializer.GetTypeInfo(typeof(SagaData));
            var second = serializer.GetTypeInfo(typeof(SagaData));

            Assert.That(second, Is.SameAs(first));
        }

        public class SagaData : ContainSagaData
        {
            public Guid DataId { get; set; }
        }

        [JsonSerializable(typeof(SagaData))]
        partial class SerializerTestsJsonContext : JsonSerializerContext;

        [JsonSerializable(typeof(Guid))]
        partial class UnrelatedJsonContext : JsonSerializerContext;
    }
}
