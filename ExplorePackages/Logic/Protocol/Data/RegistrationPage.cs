using System.Collections.Generic;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationPage
    {
        [JsonProperty("items")]
        public List<RegistrationLeafItem> Items { get; set; }
    }
}
