using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker
{
    public class SerializedMessage : ISerializedEntity
    {
        private readonly Lazy<JToken> _json;
        private readonly Lazy<string> _string;

        public SerializedMessage(Func<JToken> getJson)
        {
            _json = new Lazy<JToken>(getJson);
            _string = new Lazy<string>(() =>
            {
                return _json.Value.ToString(Formatting.None);
            });
        }

        public string AsString()
        {
            return _string.Value;
        }

        public JToken AsJToken()
        {
            return _json.Value;
        }
    }
}
