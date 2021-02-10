using System.Text;
using Microsoft.CodeAnalysis;

namespace Knapcode.ExplorePackages
{
    public class WriteTextWriterBuilder : IPropertyVisitor
    {
        private readonly int _indent;
        private readonly StringBuilder _builder;

        public WriteTextWriterBuilder(int indent)
        {
            _indent = indent;
            _builder = new StringBuilder();
        }

        public void OnProperty(GeneratorExecutionContext context, INamedTypeSymbol nullable, IPropertySymbol symbol, string prettyPropType)
        {
            if (_builder.Length > 0)
            {
                _builder.AppendLine();
                _builder.Append(' ', _indent);
                _builder.AppendLine("writer.Write(',');");
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
                    _builder.AppendFormat("writer.Write({0});", symbol.Name);
                    break;
                case "bool":
                case "bool?":
                    _builder.AppendFormat("writer.Write(CsvUtility.FormatBool({0}));", symbol.Name);
                    break;
                case "System.DateTimeOffset":
                case "System.DateTimeOffset?":
                    _builder.AppendFormat("writer.Write(CsvUtility.FormatDateTimeOffset({0}));", symbol.Name);
                    break;
                case "string":
                    _builder.AppendFormat("CsvUtility.WriteWithQuotes(writer, {0});", symbol.Name);
                    break;
                default:
                    if (symbol.Type.TypeKind == TypeKind.Enum || PropertyHelper.IsNullableEnum(nullable, symbol))
                    {
                        _builder.AppendFormat("writer.Write({0});", symbol.Name);
                    }
                    else
                    {
                        _builder.AppendFormat("CsvUtility.WriteWithQuotes(writer, {0}?.ToString());", symbol.Name);
                    }
                    break;
            }
        }

        public void Finish(GeneratorExecutionContext context)
        {
            _builder.AppendLine();
            _builder.Append(' ', _indent);
            _builder.Append("writer.WriteLine();");
        }

        public string GetResult()
        {
            return _builder.ToString();
        }
    }
}
