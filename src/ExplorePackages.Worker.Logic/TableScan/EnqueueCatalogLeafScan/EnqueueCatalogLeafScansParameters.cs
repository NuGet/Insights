using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.EnqueueCatalogLeafScan
{
    public class EnqueueCatalogLeafScansParameters
    {
        [JsonProperty("o")]
        public bool OneMessagePerId { get; set; }
    }
}