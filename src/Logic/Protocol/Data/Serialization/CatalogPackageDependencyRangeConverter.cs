// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    /// <summary>
    /// This is necessary because of this odd-ball:
    /// https://api.nuget.org/v3/catalog0/data/2016.02.21.10.24.50/dingu.generic.repo.ef7.1.0.0.json
    /// </summary>
    public class CatalogPackageDependencyRangeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }
            else if (reader.TokenType == JsonToken.String)
            {
                return (string)reader.Value;
            }
            else if (reader.TokenType == JsonToken.StartArray)
            {
                return null;
            }

            throw new JsonSerializationException($"Expected null, string, or string array.");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
