// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Insights
{
    public class AssumeUniversalDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Unexpected token {reader.TokenType} when parsing {nameof(DateTime)}.");
            }

            var result = DateTime.Parse(reader.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            if (result.Kind == DateTimeKind.Local)
            {
                return result.ToUniversalTime();
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
