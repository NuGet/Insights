// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace NuGet.Insights
{
    public class CatalogLeafItemTypeConverter : BaseCatalogLeafConverter
    {
        private static readonly IReadOnlyDictionary<CatalogLeafType, string> FromType = new Dictionary<CatalogLeafType, string>
        {
            { CatalogLeafType.PackageDelete, "nuget:PackageDelete" },
            { CatalogLeafType.PackageDetails, "nuget:PackageDetails" },
        };

        private static readonly Dictionary<string, CatalogLeafType> FromString = FromType
            .ToDictionary(x => x.Value, x => x.Key);

        public CatalogLeafItemTypeConverter() : base(FromType)
        {
        }

        public override CatalogLeafType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Unexpected token {reader.TokenType} when parsing {nameof(CatalogLeafType)}.");
            }

            if (FromString.TryGetValue(reader.GetString(), out var output))
            {
                return output;
            }

            throw new JsonException($"Unexpected value for a {nameof(CatalogLeafType)}.");
        }
    }
}
