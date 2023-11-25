// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public static class NameVersionSerializer
    {
        public static JsonSerializerOptions JsonSerializerOptions => new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(),
            },
        };

        public static ISerializedEntity SerializeData<T>(T message)
        {
            return new SerializedMessage(() => JsonSerializer.SerializeToElement(
                message,
                JsonSerializerOptions));
        }

        public static ISerializedEntity SerializeMessage<T>(string name, int version, T message)
        {
            return new SerializedMessage(() => JsonSerializer.SerializeToElement(
                new NameVersionMessage<T>(name, version, message),
                JsonSerializerOptions));
        }

        public static NameVersionMessage<JsonElement> DeserializeMessage(string message)
        {
            return JsonSerializer.Deserialize<NameVersionMessage<JsonElement>>(
                message,
                JsonSerializerOptions);
        }

        public static NameVersionMessage<JsonElement> DeserializeMessage(ReadOnlyMemory<byte> message)
        {
            return JsonSerializer.Deserialize<NameVersionMessage<JsonElement>>(
                message.Span,
                JsonSerializerOptions);
        }

        public static NameVersionMessage<JsonElement> DeserializeMessage(JsonElement message)
        {
            return JsonSerializer.Deserialize<NameVersionMessage<JsonElement>>(
                message,
                JsonSerializerOptions);
        }
    }
}
