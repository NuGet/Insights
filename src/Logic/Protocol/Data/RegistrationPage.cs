using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class RegistrationPage
    {
        [JsonProperty("items")]
        public List<RegistrationLeafItem> Items { get; set; }
    }
}
