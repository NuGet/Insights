using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ISchema<T>
    {
        string Name { get; }
        int LatestVersion { get; }
        ISerializedEntity SerializeMessage(T message);
        JToken SerializeData(T message);
        T Deserialize(int schemaVersion, JToken data);
    }
}
