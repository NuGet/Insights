using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.RunRealRestore
{
    public class RunRealRestoreCompactMessage
    {
        [JsonProperty("b")]
        public int Bucket { get; set; }
    }
}
