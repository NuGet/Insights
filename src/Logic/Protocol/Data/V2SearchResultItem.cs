using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class V2SearchResultItem
    {
        [JsonProperty("PackageRegistration")]
        public V2SearchResultPackageRegistration PackageRegistration { get; set; }

        [JsonProperty("Listed")]
        public bool Listed { get; set; }
    }
}
