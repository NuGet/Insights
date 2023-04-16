// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Worker
{
    public class SchemaSerializer
    {
        private readonly SchemaCollection _schemas;
        private readonly ILogger<SchemaSerializer> _logger;

        public SchemaSerializer(SchemaCollection schemas, ILogger<SchemaSerializer> logger)
        {
            _schemas = schemas;
            _logger = logger;
        }

        public ISchemaSerializer<T> GetSerializer<T>()
        {
            return _schemas.GetSerializer<T>();
        }

        public ISchemaSerializer GetGenericSerializer(Type type)
        {
            return _schemas.GetSerializer(type);
        }

        public ISerializedEntity Serialize<T>(T message)
        {
            return _schemas.GetSerializer<T>().SerializeMessage(message);
        }

        public ISchemaDeserializer GetDeserializer(string schemaName)
        {
            return _schemas.GetDeserializer(schemaName);
        }

        public NameVersionMessage<object> Deserialize(string message)
        {
            NameVersionMessage<object> deserializedMessage;
            try
            {
                deserializedMessage = _schemas.Deserialize(message, _logger);
            }
            catch (JsonException)
            {
                deserializedMessage = DeserializeBase64(message);
                if (deserializedMessage is null)
                {
                    throw;
                }
            }

            return deserializedMessage;
        }

        public NameVersionMessage<object> Deserialize(ReadOnlyMemory<byte> message)
        {
            NameVersionMessage<object> deserializedMessage;
            try
            {
                deserializedMessage = _schemas.Deserialize(message, _logger);
            }
            catch (JsonException)
            {
                var base64 = Encoding.ASCII.GetString(message.Span);
                deserializedMessage = DeserializeBase64(base64);
                if (deserializedMessage is null)
                {
                    throw;
                }
            }

            return deserializedMessage;
        }

        /// <summary>
        /// Try to decode the message as base64 since some tools (like Azure Storage Explorer) enqueue message content
        /// as a base64 string instead of as bytes or a raw string.
        /// </summary>
        private NameVersionMessage<object> DeserializeBase64(string base64)
        {
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                return _schemas.Deserialize(json, _logger);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is DecoderFallbackException)
            {
                return null;
            }
        }

        public NameVersionMessage<object> Deserialize(JsonElement message)
        {
            return _schemas.Deserialize(message, _logger);
        }

        public NameVersionMessage<object> Deserialize(NameVersionMessage<JsonElement> message)
        {
            return _schemas.Deserialize(message, _logger);
        }
    }
}
