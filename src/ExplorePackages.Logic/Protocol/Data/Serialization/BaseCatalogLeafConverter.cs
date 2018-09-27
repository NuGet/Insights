using System;
using System.Collections.Generic;
using Knapcode.ExplorePackages.Entities;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
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
            string output;
            if (_fromType.TryGetValue((CatalogLeafType)value, out output))
            {
                writer.WriteValue(output);
            }

            throw new NotSupportedException($"The catalog leaf type '{value}' is not supported.");
        }
    }
}
