using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public interface ISerializedEntity
    {
        JToken AsJToken();
        string AsString();
    }
}
