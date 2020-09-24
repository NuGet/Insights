using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Logic.Worker
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

        public string AsString() => _string.Value;
        public JToken AsJToken() => _json.Value;
    }
}
