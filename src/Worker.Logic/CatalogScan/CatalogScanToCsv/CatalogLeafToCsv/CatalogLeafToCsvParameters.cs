using Newtonsoft.Json;

namespace NuGet.Insights.Worker
{
    public class CatalogLeafToCsvParameters
    {
        [JsonProperty("m")]
        public CatalogLeafToCsvMode Mode { get; set; }
    }
}
