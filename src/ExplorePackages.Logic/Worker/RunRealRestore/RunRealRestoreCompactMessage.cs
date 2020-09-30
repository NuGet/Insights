using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker.RunRealRestore
{
    public class RunRealRestoreCompactMessage
    {
        [JsonProperty("b")]
        public int Bucket { get; set; }
    }
}
