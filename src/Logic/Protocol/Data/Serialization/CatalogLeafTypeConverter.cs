﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class CatalogLeafTypeConverter : BaseCatalogLeafConverter
    {
        private static readonly Dictionary<CatalogLeafType, string> FromType = new Dictionary<CatalogLeafType, string>
        {
            { CatalogLeafType.PackageDelete, "PackageDelete" },
            { CatalogLeafType.PackageDetails, "PackageDetails" },
        };

        private static readonly Dictionary<string, CatalogLeafType> FromString = FromType
            .ToDictionary(x => x.Value, x => x.Key);

        public CatalogLeafTypeConverter() : base(FromType)
        {
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            List<object> types;
            if (reader.TokenType == JsonToken.StartArray)
            {
                types = serializer.Deserialize<List<object>>(reader);
            }
            else
            {
                types = new List<object> { reader.Value };
            }

            var foundTypes = new List<CatalogLeafType>();

            foreach (var type in types.OfType<string>())
            {
                if (FromString.TryGetValue(type, out var foundType))
                {
                    foundTypes.Add(foundType);
                }
            }

            if (foundTypes.Count != 1)
            {
                throw new JsonSerializationException($"Unexpected value for a {nameof(CatalogLeafType)}.");
            }

            return foundTypes[0];
        }
    }
}
