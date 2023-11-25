// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public class SetEmptyStringsBuilder : IPropertyVisitor
    {
        private readonly StringBuilder _builder;
        private readonly int _indent;

        public SetEmptyStringsBuilder(int indent)
        {
            _indent = indent;
            _builder = new StringBuilder();
        }

        public void OnProperty(PropertyVisitorContext context, IPropertySymbol symbol, string prettyPropType)
        {
            var propertyType = symbol.Type.ToString();
            switch (propertyType)
            {
                case "string":
                    if (_builder.Length > 0)
                    {
                        _builder.AppendLine();
                        _builder.AppendLine();
                    }

                    _builder.Append(' ', _indent);
                    _builder.AppendFormat("if ({0} is null)", symbol.Name);
                    _builder.AppendLine();
                    _builder.Append(' ', _indent);
                    _builder.AppendLine("{");
                    _builder.Append(' ', _indent + 4);
                    _builder.AppendFormat("{0} = string.Empty;", symbol.Name);
                    _builder.AppendLine();
                    _builder.Append(' ', _indent);
                    _builder.Append("}");
                    break;
            }
        }

        public void Finish(PropertyVisitorContext context)
        {
        }

        public string GetResult()
        {
            return _builder.ToString();
        }
    }
}
