using System.Collections.Generic;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class CatalogPackageDependencyGroup
    {
        [JsonProperty("targetFramework")]
        public string TargetFramework { get; set; }

        [JsonProperty("dependencies")]
        public List<CatalogPackageDependency> Dependencies { get; set; }
    }
}
