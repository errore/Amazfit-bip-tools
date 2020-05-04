using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WatchFace.Parser.Attributes;

namespace WatchFace.Parser.Utils
{
    internal class ElementsHelper
    {
        public static Dictionary<byte, PropertyInfo> SortedProperties<T>()
        {
            var typeInfo = typeof(T);
            var properties = new Dictionary<byte, PropertyInfo>();
            foreach (var propertyInfo in typeInfo.GetProperties())
            {
                var parameterIdAttribute = (ParameterIdAttribute) propertyInfo
                    .GetCustomAttributes(typeof(ParameterIdAttribute), false).FirstOrDefault();
                if (parameterIdAttribute == null)
                    throw new ArgumentException(
                        $"类 {typeInfo.Name} 没有正确的ParameterIdAttribute {propertyInfo.Name}"
                    );
                if (properties.ContainsKey(parameterIdAttribute.Id))
                    throw new ArgumentException(
                        $"类 {typeInfo.Name} 已经存在ParameterIdAttribute Id为 {parameterIdAttribute.Id}"
                    );

                properties[parameterIdAttribute.Id] = propertyInfo;
            }
            return properties.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public static T GetCustomAttributeFor<T>(PropertyInfo propertyInfo)
        {
            return (T) propertyInfo.GetCustomAttributes(typeof(T), false).FirstOrDefault();
        }
    }
}