using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker
{
    public interface ISerializedEntity
    {
        JToken AsJToken();
        string AsString();
    }
}
