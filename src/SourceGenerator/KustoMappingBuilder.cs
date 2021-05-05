using System.Text;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class KustoMappingBuilder : IPropertyVisitor
    {
        private readonly int _indent;
        private readonly bool _escapeQuotes;
        private readonly StringBuilder _builder;
        private int _nextOrdinal;

        public KustoMappingBuilder(int indent, bool escapeQuotes)
        {
            _indent = indent;
            _escapeQuotes = escapeQuotes;
            _builder = new StringBuilder();
            _nextOrdinal = 0;
        }

        public void OnProperty(PropertyVisitorContext context, IPropertySymbol symbol, string prettyPropType)
        {
            var field = new DataMapping
            {
                Column = symbol.Name,
                DataType = PropertyHelper.GetKustoDataType(context, symbol),
                Properties = new CsvProperties
                {
                    Ordinal = _nextOrdinal,
                }
            };
            _nextOrdinal++;

            if (PropertyHelper.IsIgnoredInKusto(symbol))
            {
                return;
            }

            if (_builder.Length > 1)
            {
                _builder.Append(",'");
                _builder.AppendLine();
            }

            _builder.Append(' ', _indent);
            _builder.Append("'");

            var json = JsonConvert.SerializeObject(field).Replace("'", "\\'");

            if (_escapeQuotes)
            {
                json = json.Replace("\"", "\"\"");
            }

            _builder.Append(json);
        }

        public void Finish(PropertyVisitorContext context)
        {
            _builder.Append("'");
        }

        public string GetResult()
        {
            return _builder.ToString();
        }

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
