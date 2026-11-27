namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using NUnit.Framework;

    [TestFixture]
    public partial class SagaStateShapeCheckTests
    {
        [Test]
        public void Should_accept_saga_data_serialized_as_an_object_with_default_options() =>
            Assert.That(SagaStateShapeCheck.FindProblems([typeof(PlainSagaData)], null), Is.Empty);

        [Test]
        public void Should_accept_saga_data_with_metadata_from_the_serializer_options() =>
            Assert.That(SagaStateShapeCheck.FindProblems([typeof(PlainSagaData)], ShapeCheckSagaDataJsonContext.Default.Options), Is.Empty);

        [Test]
        public void Should_report_saga_data_that_a_converter_in_the_options_writes_as_a_non_object()
        {
            var options = new JsonSerializerOptions { Converters = { new WriteAsStringConverter() } };

            var problems = SagaStateShapeCheck.FindProblems([typeof(PlainSagaData)], options).ToList();

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain(typeof(PlainSagaData).FullName).And.Contain("does not serialize as a JSON object"));
        }

        [Test]
        public void Should_report_saga_data_with_a_converter_attribute() =>
            Assert.That(SagaStateShapeCheck.FindProblems([typeof(SagaDataWithTypeLevelConverter)], null).Single(), Does.Contain("does not serialize as a JSON object"));

        [Test]
        public void Should_report_saga_data_without_metadata_in_the_serializer_options() =>
            Assert.That(SagaStateShapeCheck.FindProblems([typeof(PlainSagaData)], ShapeCheckOtherJsonContext.Default.Options).Single(), Does.Contain("No JSON metadata"));

        public class PlainSagaData : ContainSagaData
        {
            public Guid DataId { get; set; }
        }

        [JsonConverter(typeof(WriteAsStringConverter))]
        public class SagaDataWithTypeLevelConverter : ContainSagaData;

        class WriteAsStringConverter : JsonConverter<ContainSagaData>
        {
            public override bool CanConvert(Type typeToConvert) => typeof(ContainSagaData).IsAssignableFrom(typeToConvert);

            public override ContainSagaData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

            public override void Write(Utf8JsonWriter writer, ContainSagaData value, JsonSerializerOptions options) => writer.WriteStringValue(value.Id.ToString());
        }

        [JsonSerializable(typeof(PlainSagaData))]
        partial class ShapeCheckSagaDataJsonContext : JsonSerializerContext;

        [JsonSerializable(typeof(Guid))]
        partial class ShapeCheckOtherJsonContext : JsonSerializerContext;
    }
}
