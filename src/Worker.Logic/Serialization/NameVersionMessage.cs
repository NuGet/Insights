using System;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker
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

        [JsonProperty("n")]
        public string SchemaName { get; }

        [JsonProperty("v")]
        public int SchemaVersion { get; }

        [JsonProperty("d")]
        public T Data { get; }
    }
}
