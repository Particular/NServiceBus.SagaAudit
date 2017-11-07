namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Runtime.CompilerServices;
    using NUnit.Framework;
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
                TimeProperty = new TimeSpan(1,2, 3, 4),
                NullableTimeProperty = new TimeSpan(5,6, 7, 8)
            };
            var serialized = SimpleJson.SerializeObject(entity, new SagaEntitySerializationStrategy());
            TestApprover.Verify(serialized);
        }

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void It_does_not_serialize_nested_objects()
        {
            var entity = new SagaEntityWithNestedObject
            {
                NestedObject = new NestedObject
                {
                    IntProperty = 42
                }
            };
            var serialized = SimpleJson.SerializeObject(entity, new SagaEntitySerializationStrategy());
            TestApprover.Verify(serialized);
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
        }
    }
}