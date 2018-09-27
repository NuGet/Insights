using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public static class CatalogJsonSerialization
    {
        public static JsonSerializer Serializer => JsonSerializer.Create(Settings);

        public static JsonSerializerSettings Settings => new JsonSerializerSettings
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateParseHandling = DateParseHandling.DateTimeOffset,
            NullValueHandling = NullValueHandling.Ignore,
        };
    }
}
