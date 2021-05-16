using System;
using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker
{
    public interface ISchemaDeserializer
    {
        string Name { get; }
        Type Type { get; }
        object Deserialize(int schemaVersion, JToken data);
    }
}
