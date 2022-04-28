// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
            return _schemas.Deserialize(message, _logger);
        }

        public NameVersionMessage<object> Deserialize(ReadOnlyMemory<byte> message)
        {
            return _schemas.Deserialize(message, _logger);
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
