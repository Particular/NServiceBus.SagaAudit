namespace NServiceBus.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using SimpleJson;
    using SimpleJson.Reflection;

    class MessageSerializationStrategy : PocoJsonSerializerStrategy
    {
        internal override IDictionary<string, ReflectionUtils.GetDelegate> GetterValueFactory(Type type)
        {
            IDictionary<string, ReflectionUtils.GetDelegate> result = new Dictionary<string, ReflectionUtils.GetDelegate>();
            foreach (var propertyInfo in ReflectionUtils.GetProperties(type))
            {
                if (propertyInfo.CanRead)
                {
                    var getMethod = ReflectionUtils.GetGetterMethodInfo(propertyInfo);
                    if (getMethod.IsStatic || !getMethod.IsPublic)
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
                output = ((TimeSpan)input).ToString("g", CultureInfo.InvariantCulture);
                return true;
            }
            output = null;
            return false;
        }
    }
}