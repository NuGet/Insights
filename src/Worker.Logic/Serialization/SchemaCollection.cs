// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker
{
    public class SchemaCollection
    {
        private readonly IReadOnlyDictionary<string, ISchemaDeserializer> _nameToSchema;
        private readonly IReadOnlyDictionary<Type, ISchemaDeserializer> _typeToSchema;

        public SchemaCollection(IReadOnlyList<ISchemaDeserializer> schemas)
        {
            _nameToSchema = schemas.ToDictionary(x => x.Name);
            _typeToSchema = schemas.ToDictionary(x => x.Type);
        }

        public ISchemaSerializer<T> GetSerializer<T>()
        {
            if (!_typeToSchema.TryGetValue(typeof(T), out var schema))
            {
                throw new FormatException($"No schema for message type '{typeof(T).FullName}' exists.");
            }

            var typedSchema = schema as ISchemaSerializer<T>;
            if (typedSchema == null)
            {
                throw new FormatException($"The schema for message type '{typeof(T).FullName}' is not a typed schema.");
            }

            return typedSchema;
        }

        public ISchemaSerializer GetSerializer(Type type)
        {
            if (!_typeToSchema.TryGetValue(type, out var schema))
            {
                throw new FormatException($"No schema for message type '{type.FullName}' exists.");
            }

            var genericSchema = schema as ISchemaSerializer;
            if (genericSchema == null)
            {
                throw new FormatException($"The schema for message type '{type.FullName}' is not a generic schema.");
            }

            return genericSchema;
        }

        public ISchemaDeserializer GetDeserializer(string schemaName)
        {
            if (!_nameToSchema.TryGetValue(schemaName, out var schema))
            {
                throw new FormatException($"The schema '{schemaName}' is not supported.");
            }

            return schema;
        }

        public NameVersionMessage<object> Deserialize(string message, ILogger logger)
        {
            return Deserialize(NameVersionSerializer.DeserializeMessage(message), logger);
        }

        public NameVersionMessage<object> Deserialize(ReadOnlyMemory<byte> message, ILogger logger)
        {
            return Deserialize(NameVersionSerializer.DeserializeMessage(message), logger);
        }

        public NameVersionMessage<object> Deserialize(JToken message, ILogger logger)
        {
            return Deserialize(NameVersionSerializer.DeserializeMessage(message), logger);
        }

        public NameVersionMessage<object> Deserialize(NameVersionMessage<JToken> message, ILogger logger)
        {
            var schema = GetDeserializer(message.SchemaName);
            var deserializedEntity = schema.Deserialize(message.SchemaVersion, message.Data);

            logger.LogInformation(
                "Deserialized object with schema {SchemaName} and version {SchemaVersion}.",
                message.SchemaName,
                message.SchemaVersion);

            return new NameVersionMessage<object>(message.SchemaName, message.SchemaVersion, deserializedEntity);
        }
    }
}
