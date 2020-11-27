using System.Collections.Generic;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class RegistrationPage
    {
        [JsonProperty("items")]
        public List<RegistrationLeafItem> Items { get; set; }
    }
}
