namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceInsight.Saga;

    [TestFixture]
    public class SagaEntitySerialization
    {
        [Test]
        public void Saga_entity_serializes_correctly()
        {
            var entity = new SagaEntity
            {
                IntProperty = 42,
                DateProperty = new DateTime(2017, 10, 26, 13, 3, 13, DateTimeKind.Utc),
                NullableDateProperty = new DateTime(2013, 10, 26, 13, 3, 13, DateTimeKind.Utc),
                GuidProperty = Guid.Empty,
                StringProperty = "String",
                TimeProperty = new TimeSpan(1, 2, 3, 4),
                NullableTimeProperty = new TimeSpan(5, 6, 7, 8),
                NestedObjectProperty = new NestedObject
                {
                    IntProperty = 1
                }
            };

            var serialized = JsonSerializer.Serialize(entity);

            Approver.Verify(serialized);
        }

        [Test]
        public void Saga_entity_compatible_with_serviceinsight()
        {
            var entity = new SagaEntity
            {
                IntProperty = 42,
                DateProperty = new DateTime(2017, 10, 26, 13, 3, 13, DateTimeKind.Utc),
                NullableDateProperty = new DateTime(2013, 10, 26, 13, 3, 13, DateTimeKind.Utc),
                GuidProperty = Guid.Empty,
                StringProperty = "String",
                TimeProperty = new TimeSpan(1, 2, 3, 4),
                NullableTimeProperty = new TimeSpan(5, 6, 7, 8),
                NestedObjectProperty = new NestedObject
                {
                    IntProperty = 1
                }
            };
            var sagaDataJson = JsonSerializer.Serialize(entity);

            var sagaDataProperties = JsonPropertiesHelper.ProcessValues(sagaDataJson);

            var jsonObj = JsonSerializer.Deserialize<JsonObject>(sagaDataJson);

            Assert.That(jsonObj, Is.Not.Null);

            foreach (var p in sagaDataProperties)
            {
                Assert.That(jsonObj.ContainsKey(p.Key), Is.True, $"{p.Key} not found");

                var expected = TrimWhitespaceAndNewLines(jsonObj[p.Key].ToString());
                var value = p.Value;

                //ServiceInsight uses default ToString() implementation, so adjust for that:
                switch (p.Key)
                {
                    case "DateProperty":
                    case "NullableDateProperty":
                        expected = DateTime.Parse(expected).ToUniversalTime().ToString();
                        break;
                    case "NestedObjectProperty":
                        value = TrimWhitespaceAndNewLines(p.Value);
                        break;
                    default:
                        break;
                }

                Assert.That(value, Is.EqualTo(expected), p.Key);
            }
        }

        string TrimWhitespaceAndNewLines(string value)
        {
            return value.Replace(Environment.NewLine, string.Empty).Replace(" ", string.Empty);
        }

        public class NestedObject
        {
            public int IntProperty { get; set; }
        }

        public class SagaEntity
        {
            public int IntProperty { get; set; }
            public string StringProperty { get; set; }
            public Guid GuidProperty { get; set; }
            public DateTime DateProperty { get; set; }
            public DateTime? NullableDateProperty { get; set; }
            public TimeSpan TimeProperty { get; set; }
            public TimeSpan? NullableTimeProperty { get; set; }
            public NestedObject NestedObjectProperty { get; set; }
            public static string StaticProperty { get; set; } = "test";
#pragma warning disable IDE0051 // Remove unused private members
            string PrivateProperty { get; set; } = "test";
#pragma warning restore IDE0051 // Remove unused private members
        }
    }
}

// This is copied from ServiceInsight to make sure that we are compatible. Note that its using Newtonsoft.Json
namespace ServiceInsight.Saga
{
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;

    class JsonPropertiesHelper
    {
        static readonly IList<string> StandardKeys = ["$type", "Id", "Originator", "OriginalMessageId"];

        public static IList<KeyValuePair<string, string>> ProcessValues(string stateAfterChange) => JsonConvert.DeserializeObject<Dictionary<string, object>>(stateAfterChange)
                  .Where(m => StandardKeys.All(s => s != m.Key))
                  .Select(f => new KeyValuePair<string, string>(f.Key, f.Value?.ToString()))
                  .ToList();

        public static IList<KeyValuePair<string, string>> ProcessArray(string stateAfterChange) => ProcessValues(stateAfterChange.TrimStart('[').TrimEnd(']'));
    }
}