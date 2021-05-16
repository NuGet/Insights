using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class RegistrationLeaf
    {
        [JsonProperty("catalogEntry")]
        public string CatalogEntry { get; set; }

        [JsonProperty("listed")]
        public bool Listed { get; set; }
    }
}
