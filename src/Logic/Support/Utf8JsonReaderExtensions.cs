// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public static class Utf8JsonReaderExtensions
    {
        public static void AssertType(ref this Utf8JsonReader reader, JsonTokenType type)
        {
            if (reader.TokenType != type)
            {
                throw new JsonException($"Expected a {type} token, not a {reader.TokenType} token.");
            }
        }

        public static void AssertReadAndType(ref this Utf8JsonReader reader, JsonTokenType type)
        {
            if (!reader.Read())
            {
                throw new JsonException($"Expected a {type} token, not the end of the stream.");
            }

            reader.AssertType(type);
        }

        public static void AssertRead(ref this Utf8JsonReader reader)
        {
            if (!reader.Read())
            {
                throw new JsonException($"Expected a token, not the end of the stream.");
            }
        }
    }
}
