// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Toolkit.HighPerformance;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker
{
    public static class NameVersionSerializer
    {
        public static JsonSerializerSettings JsonSerializerSettings => new JsonSerializerSettings
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateParseHandling = DateParseHandling.DateTimeOffset,
            NullValueHandling = NullValueHandling.Ignore,
            Converters =
            {
                new StringEnumConverter(),
            },
        };

        public static JsonSerializer JsonSerializer => JsonSerializer.Create(JsonSerializerSettings);

        public static ISerializedEntity SerializeData<T>(T message)
        {
            return new SerializedMessage(() => JToken.FromObject(
                message,
                JsonSerializer));
        }

        public static ISerializedEntity SerializeMessage<T>(string name, int version, T message)
        {
            return new SerializedMessage(() => JToken.FromObject(
                new NameVersionMessage<T>(name, version, message),
                JsonSerializer));
        }

        public static NameVersionMessage<JToken> DeserializeMessage(string message)
        {
            return JsonConvert.DeserializeObject<NameVersionMessage<JToken>>(
                message,
                JsonSerializerSettings);
        }

        public static NameVersionMessage<JToken> DeserializeMessage(ReadOnlyMemory<byte> message)
        {
            using (var stream = message.AsStream())
            using (var streamReader = new StreamReader(stream))
            using (var textReader = new JsonTextReader(streamReader))
            {
                return JsonSerializer.Deserialize<NameVersionMessage<JToken>>(textReader);
            }
        }

        public static NameVersionMessage<JToken> DeserializeMessage(JToken message)
        {
            return message.ToObject<NameVersionMessage<JToken>>(JsonSerializer);
        }
    }
}
