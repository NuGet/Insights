using Newtonsoft.Json;

namespace NuGet.Insights
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
