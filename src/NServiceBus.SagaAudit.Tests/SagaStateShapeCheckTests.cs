namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using NUnit.Framework;

    [TestFixture]
    public partial class SagaStateShapeCheckTests
    {
        [Test]
        public void Should_accept_saga_data_serialized_as_an_object_with_default_options() =>
            Assert.That(FindProblemMessages([typeof(PlainSagaData)], null), Is.Empty);

        [Test]
        public void Should_accept_saga_data_with_metadata_from_the_serializer_options() =>
            Assert.That(FindProblemMessages([typeof(PlainSagaData)], ShapeCheckSagaDataJsonContext.Default.Options), Is.Empty);

        [Test]
        public void Should_report_saga_data_that_a_converter_in_the_options_writes_as_a_non_object()
        {
            var options = new JsonSerializerOptions { Converters = { new WriteAsStringConverter() } };

            var problems = FindProblemMessages([typeof(PlainSagaData)], options).ToList();

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain(typeof(PlainSagaData).FullName).And.Contain("does not serialize as a JSON object"));
        }

        [Test]
        public void Should_report_saga_data_with_a_converter_attribute() =>
            Assert.That(FindProblemMessages([typeof(SagaDataWithTypeLevelConverter)], null).Single(), Does.Contain("does not serialize as a JSON object"));

        [Test]
        public void Should_report_saga_data_without_metadata_in_the_serializer_options() =>
            Assert.That(FindProblemMessages([typeof(PlainSagaData)], ShapeCheckOtherJsonContext.Default.Options).Single(), Does.Contain("No JSON metadata"));

        [Test]
        public void Should_report_standard_properties_renamed_by_a_naming_policy()
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            var problem = FindProblemMessages([typeof(PlainSagaData)], options).Single();

            Assert.That(problem, Does.Contain("'Id' as 'id'").And.Contain("'Originator' as 'originator'").And.Contain("'OriginalMessageId' as 'originalMessageId'"));
        }

        [Test]
        public void Should_report_standard_properties_renamed_by_source_generated_metadata() =>
            Assert.That(FindProblemMessages([typeof(PlainSagaData)], ShapeCheckSnakeCaseJsonContext.Default.Options).Single(), Does.Contain("'OriginalMessageId' as 'original_message_id'"));

        [Test]
        public void Should_report_a_standard_property_renamed_by_an_attribute()
        {
            var problem = FindProblemMessages([typeof(SagaDataWithRenamedId)], null).Single();

            Assert.That(problem, Does.Contain("'Id' as 'sagaId'").And.Not.Contain("Originator"));
        }

        [Test]
        public void Should_not_modify_options_shared_with_other_components()
        {
            var reflectionOptions = new JsonSerializerOptions();
            var resolverOptions = new JsonSerializerOptions { TypeInfoResolver = ShapeCheckSagaDataJsonContext.Default };

            _ = FindProblemMessages([typeof(PlainSagaData)], reflectionOptions).ToList();
            _ = FindProblemMessages([typeof(PlainSagaData)], resolverOptions).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(reflectionOptions.IsReadOnly, Is.False);
                Assert.That(reflectionOptions.TypeInfoResolver, Is.Null);
                Assert.That(resolverOptions.IsReadOnly, Is.False);
            }

            Assert.DoesNotThrow(() => reflectionOptions.Converters.Add(new WriteAsStringConverter()));
        }

        [Test]
        public void Should_flag_only_serialization_failures_as_preventing_serialization()
        {
            var missing = SagaStateShapeCheck.FindProblems([typeof(PlainSagaData)], new SagaStateSerializer(ShapeCheckOtherJsonContext.Default.Options)).Single();
            var notAnObject = SagaStateShapeCheck.FindProblems([typeof(SagaDataWithTypeLevelConverter)], new SagaStateSerializer(null)).Single();
            var renamed = SagaStateShapeCheck.FindProblems([typeof(SagaDataWithRenamedId)], new SagaStateSerializer(null)).Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(missing.PreventsSerialization, Is.True);
                Assert.That(notAnObject.PreventsSerialization, Is.False);
                Assert.That(renamed.PreventsSerialization, Is.False);
            }
        }

        [Test]
        public void Should_report_an_invalid_contract_instead_of_throwing()
        {
            var problem = SagaStateShapeCheck.FindProblems([typeof(SagaDataWithCollidingNames)], new SagaStateSerializer(null)).Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(problem.PreventsSerialization, Is.True);
                Assert.That(problem.Message, Does.Contain(typeof(SagaDataWithCollidingNames).FullName).And.Contain("cannot be serialized"));
            }
        }

        static IEnumerable<string> FindProblemMessages(IEnumerable<Type> sagaDataTypes, JsonSerializerOptions? options) =>
            SagaStateShapeCheck.FindProblems(sagaDataTypes, new SagaStateSerializer(options)).Select(problem => problem.Message);

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

        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
        [JsonSerializable(typeof(PlainSagaData))]
        partial class ShapeCheckSnakeCaseJsonContext : JsonSerializerContext;

        public class SagaDataWithCollidingNames : ContainSagaData
        {
            [JsonPropertyName("same")]
            public int First { get; set; }

            [JsonPropertyName("same")]
            public int Second { get; set; }
        }

        public class SagaDataWithRenamedId : ContainSagaData
        {
            [JsonPropertyName("sagaId")]
            public override Guid Id { get; set; }
        }
    }
}
