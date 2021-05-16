using System;
using System.Globalization;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class AssumeUniversalDateTimeOffsetConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTimeOffset);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.String)
            {
                throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing {nameof(DateTimeOffset)}.");
            }

            return DateTimeOffset.Parse((string)reader.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
