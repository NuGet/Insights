using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class RegistrationCatalogEntry
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("listed")]
        public bool Listed { get; set; }
    }
}
