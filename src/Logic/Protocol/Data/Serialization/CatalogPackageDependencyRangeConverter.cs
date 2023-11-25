// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    /// <summary>
    /// This is necessary because of this odd-ball:
    /// https://api.nuget.org/v3/catalog0/data/2016.02.21.10.24.50/dingu.generic.repo.ef7.1.0.0.json
    /// </summary>
    public class CatalogPackageDependencyRangeConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                reader.Skip();
                return null;
            }

            throw new JsonException($"Expected null, string, or string array.");
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
