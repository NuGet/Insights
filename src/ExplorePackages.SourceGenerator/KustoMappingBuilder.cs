using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class KustoMappingBuilder : IPropertyVisitor
    {
        private readonly List<FieldMapping> _fields;

        public KustoMappingBuilder()
        {
            _fields = new List<FieldMapping>();
        }
        public void OnProperty(IPropertySymbol symbol, string prettyPropType)
        {
            _fields.Add(new FieldMapping
            {
                Name = symbol.Name,
                Ordinal = _fields.Count,
                DataType = PropertyHelper.GetKustoDataType(symbol),
            });
        }

        public void Finish()
        {
        }

        public string GetResult() => JsonConvert.SerializeObject(_fields);

        private class FieldMapping
        {
            public string Name { get; set; }
            public int Ordinal { get; set; }
            public string DataType { get; set; }
        }
    }
}
