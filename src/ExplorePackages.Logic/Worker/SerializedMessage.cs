using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class SerializedMessage : ISerializedMessage
    {
        private readonly Lazy<JToken> _json;
        private readonly Lazy<byte[]> _bytes;

        public SerializedMessage(Func<JToken> getJson)
        {
            _json = new Lazy<JToken>(getJson);
            _bytes = new Lazy<byte[]>(() =>
            {
                var json = _json.Value.ToString(Formatting.None);
                return Encoding.UTF8.GetBytes(json);
            });
        }

        public byte[] AsBytes() => _bytes.Value;
        public JToken AsJToken() => _json.Value;
    }
}
