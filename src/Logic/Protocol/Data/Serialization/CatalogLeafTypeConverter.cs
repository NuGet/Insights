// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace NuGet.Insights
{
    public class CatalogLeafTypeConverter : BaseCatalogLeafConverter
    {
        private static readonly IReadOnlyDictionary<CatalogLeafType, string> FromType = new Dictionary<CatalogLeafType, string>
        {
            { CatalogLeafType.PackageDelete, "PackageDelete" },
            { CatalogLeafType.PackageDetails, "PackageDetails" },
        };

        private static readonly IReadOnlySet<string> IgnoredTypes = new HashSet<string> { "catalog:Permalink" };

        private static readonly Dictionary<string, CatalogLeafType> FromString = FromType
            .ToDictionary(x => x.Value, x => x.Key);

        public CatalogLeafTypeConverter() : base(FromType)
        {
        }

        public override CatalogLeafType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var types = new List<string>();
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                reader.Read();
                while (reader.TokenType == JsonTokenType.String)
                {
                    types.Add(reader.GetString());
                    reader.Read();
                }

                if (reader.TokenType != JsonTokenType.EndArray)
                {
                    throw new JsonException($"Expected end of array after catalog leaf type strings, not a {reader.TokenType}.");
                }
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                types.Add(reader.GetString());
            }
            else
            {
                throw new JsonException($"Expected start of array or string for catalog leaf type strings, not a {reader.TokenType}.");
            }

            var foundTypes = new List<CatalogLeafType>();

            foreach (var type in types)
            {
                if (FromString.TryGetValue(type, out var foundType))
                {
                    foundTypes.Add(foundType);
                }
                else if (IgnoredTypes.Contains(type))
                {
                    continue;
                }
                else
                {
                    throw new JsonException($"Unexpected value for a {nameof(CatalogLeafType)}.");
                }
            }

            if (foundTypes.Count != 1)
            {
                throw new JsonException("Expected exactly one catalog leaf type.");
            }

            return foundTypes[0];
        }
    }
}
