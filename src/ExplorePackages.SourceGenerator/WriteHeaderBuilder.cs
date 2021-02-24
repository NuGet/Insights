using System.Text;
using Microsoft.CodeAnalysis;

namespace Knapcode.ExplorePackages
{
    public class WriteHeaderBuilder : IPropertyVisitor
    {
        private readonly int _indent;
        private readonly StringBuilder _builder;

        public WriteHeaderBuilder(int indent)
        {
            _indent = indent;
            _builder = new StringBuilder();
        }

        public void OnProperty(GeneratorExecutionContext context, INamedTypeSymbol nullable, IPropertySymbol symbol, string prettyPropType)
        {
            if (_builder.Length == 0)
            {
                _builder.Append(' ', _indent);
                _builder.Append("writer.WriteLine(\"");
            }
            else
            {
                _builder.Append(',');
            }

            _builder.Append(symbol.Name);
        }

        public void Finish(GeneratorExecutionContext context)
        {
            _builder.Append("\");");
        }

        public string GetResult()
        {
            return _builder.ToString();
        }
    }
}
