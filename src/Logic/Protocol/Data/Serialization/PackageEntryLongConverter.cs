// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    /// <summary>
    /// This is necessary because of this odd-ball:
    /// https://api.nuget.org/v3/catalog0/data/2019.11.26.10.59.56/paket.core.5.237.1.json
    /// </summary>
    public class PackageEntryLongConverter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt64();
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                reader.Read();
                reader.AssertReadAndType(JsonTokenType.Number);
                var firstNumber = reader.GetInt64();
                do
                {
                    reader.AssertRead();
                }
                while (reader.TokenType != JsonTokenType.EndArray);

                return firstNumber;
            }

            throw new JsonException($"Expected number or number array.");
        }

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }
}
