// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;

namespace NuGet.Insights.Worker
{
    public class TableEntityJsonConverter : JsonConverter<TableEntity>
    {
        public override TableEntity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }

        public override void Write(Utf8JsonWriter writer, TableEntity value, JsonSerializerOptions options)
        {
            var sortedDictionary = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var property in value)
            {
                sortedDictionary.Add(property.Key, property.Value);
            }

            JsonSerializer.Serialize(writer, sortedDictionary, options);
        }
    }
}
