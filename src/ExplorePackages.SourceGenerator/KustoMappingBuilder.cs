using System.Text;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class KustoMappingBuilder : IPropertyVisitor
    {
        private readonly int _indent;
        private readonly StringBuilder _builder;
        private int _nextOrdinal;

        public KustoMappingBuilder(int indent)
        {
            _indent = indent;
            _builder = new StringBuilder();
            _nextOrdinal = 0;
        }

        public void OnProperty(IPropertySymbol symbol, string prettyPropType)
        {
            var field = new FieldMapping
            {
                Name = symbol.Name,
                Ordinal = _nextOrdinal,
                DataType = PropertyHelper.GetKustoDataType(symbol),
            };
            _nextOrdinal++;

            if (_builder.Length > 1)
            {
                _builder.Append(",'");
                _builder.AppendLine();
            }

            _builder.Append(' ', _indent);
            _builder.Append("'");
            _builder.Append(JsonConvert.SerializeObject(field).Replace("'", "\\'"));
        }

        public void Finish()
        {
            _builder.Append("'");
        }

        public string GetResult() => _builder.ToString();

        private class FieldMapping
        {
            public string Name { get; set; }
            public int Ordinal { get; set; }
            public string DataType { get; set; }
        }
    }
}
