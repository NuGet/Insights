// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public abstract class BaseCatalogLeafConverter : JsonConverter<CatalogLeafType>
    {
        private readonly IReadOnlyDictionary<CatalogLeafType, string> _fromType;

        public BaseCatalogLeafConverter(IReadOnlyDictionary<CatalogLeafType, string> fromType)
        {
            _fromType = fromType;
        }

        public override void Write(Utf8JsonWriter writer, CatalogLeafType value, JsonSerializerOptions options)
        {
            if (_fromType.TryGetValue(value, out var output))
            {
                writer.WriteStringValue(output);
                return;
            }

            throw new NotSupportedException($"The catalog leaf type '{value}' is not supported.");
        }
    }
}
