// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class NameVersionMessage<T>
    {
        [JsonConstructor]
        public NameVersionMessage(string schemaName, int schemaVersion, T data)
        {
            SchemaName = schemaName ?? throw new ArgumentNullException(nameof(schemaName));
            SchemaVersion = schemaVersion;
            Data = data;
        }

        [JsonPropertyName("n")]
        public string SchemaName { get; }

        [JsonPropertyName("v")]
        public int SchemaVersion { get; }

        [JsonPropertyName("d")]
        public T Data { get; }
    }
}
