using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public interface ISerializedMessage
    {
        JToken AsJToken();
        byte[] AsBytes();
    }
}
