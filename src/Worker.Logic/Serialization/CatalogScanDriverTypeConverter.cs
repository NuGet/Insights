// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker
{
    public class CatalogScanDriverTypeConverter : JsonConverter<CatalogScanDriverType>
    {
        public override CatalogScanDriverType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (str is null)
            {
                throw new FormatException($"A string was expected when parsing a {nameof(CatalogScanDriverType)} from JSON.");
            }

            return CatalogScanDriverType.Parse(str);
        }

        public override void Write(Utf8JsonWriter writer, CatalogScanDriverType value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
