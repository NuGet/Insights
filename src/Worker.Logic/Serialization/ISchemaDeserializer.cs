using System;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ISchemaDeserializer
    {
        string Name { get; }
        Type Type { get; }
        object Deserialize(int schemaVersion, JToken data);
    }
}
