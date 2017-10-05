namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using SimpleJson;
    using SimpleJson.Reflection;

    class SagaEntitySerializationStrategy : PocoJsonSerializerStrategy
    {
        protected override bool TrySerializeUnknownTypes(object input, out object output)
        {
            var baseResult = base.TrySerializeUnknownTypes(input, out output);
            if (baseResult)
            {
                var obj = (JsonObject)output;
                var entityTypeName = typeNames.GetOrAdd(input.GetType(), t =>
                {
                    var sagaEntityTypeName = t.AssemblyQualifiedName;
                    var nameParts = sagaEntityTypeName.Split(new[]
                    {
                            ','
                    }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
                    var nameAndAssembly = $"{nameParts[0]}, {nameParts[1]}";
                    return nameAndAssembly;
                });

                obj["$type"] = entityTypeName;
            }
            return baseResult;
        }

        internal override IDictionary<string, ReflectionUtils.GetDelegate> GetterValueFactory(Type type)
        {
            IDictionary<string, ReflectionUtils.GetDelegate> result = new Dictionary<string, ReflectionUtils.GetDelegate>();
            foreach (var propertyInfo in ReflectionUtils.GetProperties(type))
            {
                if (propertyInfo.CanRead)
                {
                    var getMethod = ReflectionUtils.GetGetterMethodInfo(propertyInfo);
                    if (getMethod.IsStatic || !getMethod.IsPublic || !IsSerializableType(propertyInfo.PropertyType))
                    {
                        continue;
                    }
                    result[MapClrMemberNameToJsonFieldName(propertyInfo.Name)] = ReflectionUtils.GetGetMethod(propertyInfo);
                }
            }
            return result;
        }

        protected override bool TrySerializeKnownTypes(object input, out object output)
        {
            return base.TrySerializeKnownTypes(input, out output) || TrySerializeOtherKnownTypes(input, out output);
        }

        static bool TrySerializeOtherKnownTypes(object input, out object output)
        {
            if (input is TimeSpan)
            {
                output = ((TimeSpan)input).ToString("g");
                return true;
            }
            output = null;
            return false;
        }

        static bool IsSerializableType(Type t)
        {
            return t.IsPrimitive || t == typeof(string) || t == typeof(Guid);
        }

        static ConcurrentDictionary<Type, string> typeNames = new ConcurrentDictionary<Type, string>();
    }
}