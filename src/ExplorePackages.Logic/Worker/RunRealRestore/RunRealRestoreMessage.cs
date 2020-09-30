using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic.Worker.RunRealRestore
{
    public class RunRealRestoreMessage
    {
        [JsonProperty("i")]
        public string Id { get; set; }

        [JsonProperty("v")]
        public string Version { get; set; }

        [JsonProperty("f")]
        public string Framework { get; set; }

        [JsonProperty("tn")]
        public string TemplateName { get; set; }

        [JsonProperty("tpi")]
        public string TemplatePackageId { get; set; }

        [JsonProperty("tpv")]
        public string TemplatePackageVersion { get; set; }
    }
}
