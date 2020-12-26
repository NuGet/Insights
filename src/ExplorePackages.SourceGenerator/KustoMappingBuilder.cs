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

        public void OnProperty(INamedTypeSymbol nullable, IPropertySymbol symbol, string prettyPropType)
        {
            var field = new DataMapping
            {
                Column = symbol.Name,
                DataType = PropertyHelper.GetKustoDataType(nullable, symbol),
                Properties = new CsvProperties
                {
                    Ordinal = _nextOrdinal,
                }
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

        /// <summary>
        /// Source: https://docs.microsoft.com/en-us/azure/data-explorer/kusto/management/mappings
        /// </summary>
        private class DataMapping
        {
            public string Column { get; set; }

            public string DataType { get; set; }

            public CsvProperties Properties { get; set; }
        }

        private class CsvProperties
        {
            public int Ordinal { get; set; }
        }
    }
}
