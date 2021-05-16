using Newtonsoft.Json;

namespace NuGet.Insights.Worker.EnqueueCatalogLeafScan
{
    public class EnqueueCatalogLeafScansParameters
    {
        [JsonProperty("o")]
        public bool OneMessagePerId { get; set; }
    }
}