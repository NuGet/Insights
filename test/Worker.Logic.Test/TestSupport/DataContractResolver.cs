// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Serialization;

#nullable enable

namespace NuGet.Insights.Worker
{
    /// <summary>
    /// Inspired by: https://github.com/dotnet/runtime/issues/29975#issuecomment-1187188015
    /// </summary>
    public class DataContractResolver : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

            if (jsonTypeInfo.Kind == JsonTypeInfoKind.Object)
            {
                jsonTypeInfo.Properties.Clear();

                var properties = new List<JsonPropertyInfo>();

                foreach (PropertyInfo propInfo in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (propInfo.GetCustomAttribute<IgnoreDataMemberAttribute>() is not null)
                    {
                        continue;
                    }

                    var dataMember = propInfo.GetCustomAttribute<DataMemberAttribute>();
                    var propertyInfo = jsonTypeInfo.CreateJsonPropertyInfo(propInfo.PropertyType, dataMember?.Name ?? propInfo.Name);
                    propertyInfo.Get = propInfo.CanRead ? propInfo.GetValue : null;
                    propertyInfo.Set = propInfo.CanWrite ? propInfo.SetValue : null;

                    properties.Add(propertyInfo);
                }

                foreach (var property in properties.OrderBy(x => x.Order).ThenBy(x => x.Name, StringComparer.Ordinal))
                {
                    jsonTypeInfo.Properties.Add(property);
                }
            }

            return jsonTypeInfo;
        }
    }
}
