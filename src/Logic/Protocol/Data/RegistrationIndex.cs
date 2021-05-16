using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class RegistrationIndex
    {
        [JsonProperty("items")]
        public List<RegistrationPageItem> Items { get; set; }
    }
}
