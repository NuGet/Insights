using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public static class NameVersionSerializer
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateParseHandling = DateParseHandling.DateTimeOffset,
            NullValueHandling = NullValueHandling.Ignore,
            Converters =
            {
                new StringEnumConverter(),
            },
        };

        public static readonly JsonSerializer JsonSerializer = JsonSerializer.Create(JsonSerializerSettings);

        public static ISerializedEntity SerializeData<T>(T message)
        {
            return new SerializedMessage(() => JToken.FromObject(
                message,
                JsonSerializer));
        }

        public static ISerializedEntity SerializeMessage<T>(string name, int version, T message)
        {
            return new SerializedMessage(() => JToken.FromObject(
                new NameVersionMessage<T>(name, version, message),
                JsonSerializer));
        }

        public static NameVersionMessage<JToken> DeserializeMessage(string message)
        {
            return JsonConvert.DeserializeObject<NameVersionMessage<JToken>>(
                message,
                JsonSerializerSettings);
        }

        public static NameVersionMessage<JToken> DeserializeMessage(JToken message)
        {
            return message.ToObject<NameVersionMessage<JToken>>(JsonSerializer);
        }
    }
}
