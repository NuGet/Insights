using System.Text;
using Microsoft.CodeAnalysis;

namespace Knapcode.ExplorePackages
{
    public class WriteAsyncBuilder : IPropertyVisitor
    {
        private readonly int _indent;
        private readonly StringBuilder _builder;

        public WriteAsyncBuilder(int indent)
        {
            _indent = indent;
            _builder = new StringBuilder();
        }

        public void OnProperty(INamedTypeSymbol nullable, IPropertySymbol symbol, string prettyPropType)
        {
            if (_builder.Length > 0)
            {
                _builder.AppendLine();
                _builder.Append(' ', _indent);
                _builder.AppendLine("await writer.WriteAsync(',');");
            }

            _builder.Append(' ', _indent);

            switch (symbol.Type.ToString())
            {
                case "int":
                case "int?":
                case "long":
                case "long?":
                case "System.Guid":
                case "System.Guid?":
                case "System.TimeSpan":
                case "System.TimeSpan?":
                case "System.Version":
                    _builder.AppendFormat("await writer.WriteAsync({0}.ToString());", symbol.Name);
                    break;
                case "bool":
                case "bool?":
                    _builder.AppendFormat("await writer.WriteAsync(CsvUtility.FormatBool({0}));", symbol.Name);
                    break;
                case "System.DateTimeOffset":
                case "System.DateTimeOffset?":
                    _builder.AppendFormat("await writer.WriteAsync(CsvUtility.FormatDateTimeOffset({0}));", symbol.Name);
                    break;
                case "string":
                    _builder.AppendFormat("await CsvUtility.WriteWithQuotesAsync(writer, {0});", symbol.Name);
                    break;
                default:
                    if (symbol.Type.TypeKind == TypeKind.Enum || PropertyHelper.IsNullableEnum(nullable, symbol))
                    {
                        _builder.AppendFormat("await writer.WriteAsync({0}.ToString());", symbol.Name);
                    }
                    else
                    {
                        _builder.AppendFormat("await CsvUtility.WriteWithQuotesAsync(writer, {0}?.ToString());", symbol.Name);
                    }
                    break;
            }
        }

        public void Finish()
        {
            _builder.AppendLine();
            _builder.Append(' ', _indent);
            _builder.Append("await writer.WriteLineAsync();");
        }

        public string GetResult()
        {
            return _builder.ToString();
        }
    }
}
