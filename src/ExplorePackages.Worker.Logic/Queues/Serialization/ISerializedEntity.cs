using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ISerializedEntity
    {
        JToken AsJToken();
        string AsString();
    }
}
