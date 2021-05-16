using System;
using System.Linq;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public class FirstStringConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }
            else if (reader.TokenType == JsonToken.String)
            {
                return (string)reader.Value;
            }
            else if (reader.TokenType == JsonToken.StartArray)
            {
                return serializer.Deserialize<string[]>(reader).FirstOrDefault();
            }

            throw new JsonSerializationException($"Expected null, string, or string array.");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
