using System.Collections.Generic;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationIndex
    {
        [JsonProperty("items")]
        public List<RegistrationPageItem> Items { get; set; }
    }
}
