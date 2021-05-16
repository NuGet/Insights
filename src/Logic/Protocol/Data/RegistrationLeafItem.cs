using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class RegistrationLeafItem
    {
        [JsonProperty("catalogEntry")]
        public RegistrationCatalogEntry CatalogEntry { get; set; }
    }
}
