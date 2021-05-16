using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Insights
{
    public abstract class BaseCatalogLeafConverter : JsonConverter
    {
        private readonly IReadOnlyDictionary<CatalogLeafType, string> _fromType;

        public BaseCatalogLeafConverter(IReadOnlyDictionary<CatalogLeafType, string> fromType)
        {
            _fromType = fromType;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(CatalogLeafType);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (_fromType.TryGetValue((CatalogLeafType)value, out var output))
            {
                writer.WriteValue(output);
                return;
            }

            throw new NotSupportedException($"The catalog leaf type '{value}' is not supported.");
        }
    }
}
