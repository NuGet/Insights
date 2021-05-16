using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class V2SearchResultPackageRegistration
    {
        [JsonProperty("Id")]
        public string Id { get; set; }
    }
}
