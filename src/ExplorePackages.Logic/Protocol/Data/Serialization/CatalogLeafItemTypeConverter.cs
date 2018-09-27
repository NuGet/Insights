using System;
using System.Collections.Generic;
using System.Linq;
using Knapcode.ExplorePackages.Entities;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogLeafItemTypeConverter : BaseCatalogLeafConverter
    {
        private static readonly Dictionary<CatalogLeafType, string> FromType = new Dictionary<CatalogLeafType, string>
        {
            { CatalogLeafType.PackageDelete, "nuget:PackageDelete" },
            { CatalogLeafType.PackageDetails, "nuget:PackageDetails" },
        };

        private static readonly Dictionary<string, CatalogLeafType> FromString = FromType
            .ToDictionary(x => x.Value, x => x.Key);

        public CatalogLeafItemTypeConverter() : base(FromType)
        {
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string stringValue = reader.Value as string;
            if (stringValue != null)
            {
                CatalogLeafType output;
                if (FromString.TryGetValue(stringValue, out output))
                {
                    return output;
                }
            }

            throw new JsonSerializationException($"Unexpected value for a {nameof(CatalogLeafType)}.");
        }
    }
}
