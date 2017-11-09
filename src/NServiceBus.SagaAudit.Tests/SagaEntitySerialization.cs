namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using ApprovalUtilities.Utilities;
    using NUnit.Framework;
    using ServiceInsight.Saga;
    using Settings;
    using SimpleJson;

    [TestFixture]
    public class SagaEntitySerialization
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
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
            var serialized = SimpleJson.SerializeObject(entity, new SagaEntitySerializationStrategy());
            TestApprover.Verify(serialized);
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
            var sagaDataJson = SimpleJson.SerializeObject(entity, new SagaEntitySerializationStrategy());

            var sagaDataProperties = JsonPropertiesHelper.ProcessValues(sagaDataJson);

            var jsonObj = SimpleJson.DeserializeObject(sagaDataJson) as JsonObject;

            Assert.IsNotNull(jsonObj, "SimpleJson.DeserializeObject");

            sagaDataProperties.ForEach(p =>
            {
                Assert.IsTrue(jsonObj.ContainsKey(p.Key), $"{p.Key} not found");

                var expected = jsonObj[p.Key].ToString();
                var value = p.Value;

                //ServiceInsight uses default ToString() implementation, so adjust for that:
                switch (p.Key)
                {
                    case "DateProperty":
                    case "NullableDateProperty":
                        // ReSharper disable once SpecifyACultureInStringConversionExplicitly
                        expected = DateTime.Parse(expected).ToUniversalTime().ToString();
                        break;
                    case "NestedObjectProperty":
                        value = p.Value.Replace(Environment.NewLine, string.Empty).Replace(" ", string.Empty).Replace(",NServiceBus", ", NServiceBus");
                        break;
                }

                Assert.AreEqual(expected, value, p.Key);
            });
        }

        [Test]
        public void Saga_entity_compatible_with_legacy_plugin()
        {
            var entity = new SagaEntity
            {
                IntProperty = 42,
                DateProperty = new DateTime(2017, 10, 26, 13, 3, 13, DateTimeKind.Utc),
                NullableDateProperty = new DateTime(2013, 10, 26, 13, 3, 13, DateTimeKind.Utc),
                GuidProperty = Guid.Empty,
                StringProperty = "String",
                TimeProperty = new TimeSpan(1, 2, 3, 4),
                NullableTimeProperty = new TimeSpan(5, 6, 7, 8)
            };
            var sagaDataJson = SimpleJson.SerializeObject(new[] { entity }, new SagaEntitySerializationStrategy());
            var v6Serializer = new ServiceControl.Plugin.SagaAudit.SagaAuditSerializer(new SettingsHolder());
            var legacySagaDataJson = v6Serializer.Serialize(entity);

            var sagaDataProperties = JsonPropertiesHelper.ProcessArray(sagaDataJson);
            var legacySagaDataProperties = JsonPropertiesHelper.ProcessArray(legacySagaDataJson);

            sagaDataProperties.ForEach(p =>
            {
                var legacyProperty = legacySagaDataProperties.SingleOrDefault(lp => lp.Key == p.Key);
                Assert.IsNotNull(legacyProperty, $"{p.Key} not found");
                Assert.AreEqual(legacyProperty.Value, p.Value, p.Key);
            });
        }

        public class SagaEntityWithNestedObject
        {
            public NestedObject NestedObject { get; set; }
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
            string PrivateProperty { get; set; } = "test";
        }
    }
}

namespace ServiceInsight.Saga
{
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;

    class JsonPropertiesHelper
    {
        static readonly IList<string> StandardKeys = new List<string> { "$type", "Id", "Originator", "OriginalMessageId" };

        public static IList<KeyValuePair<string, string>> ProcessValues(string stateAfterChange) => JsonConvert.DeserializeObject<Dictionary<string, object>>(stateAfterChange)
            .Where(m => StandardKeys.All(s => s != m.Key))
            .Select(f => new KeyValuePair<string, string>(f.Key, f.Value?.ToString()))
            .ToList();

        public static IList<KeyValuePair<string, string>> ProcessArray(string stateAfterChange) => ProcessValues(stateAfterChange.TrimStart('[').TrimEnd(']'));
    }
}

namespace ServiceControl.Plugin.SagaAudit
{
    using System.IO;
    using NServiceBus;
    using NServiceBus.MessageInterfaces.MessageMapper.Reflection;
    using NServiceBus.Serialization;
    using NServiceBus.Settings;

    class SagaAuditSerializer
    {
        readonly IMessageSerializer serializer;

        public SagaAuditSerializer(ReadOnlySettings settings)
        {
            var definition = new JsonSerializer();

            var factory = definition.Configure(settings);

            serializer = factory(new MessageMapper());
        }

        public string Serialize<T>(T entity)
        {
            using (var memoryStream = new MemoryStream())
            {
                serializer.Serialize(new[]
                {
                    entity
                }, memoryStream);

                memoryStream.Position = 0;
                using (var streamReader = new StreamReader(memoryStream))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }
    }
}