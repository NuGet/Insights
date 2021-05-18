// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public class WriteListBuilder : IPropertyVisitor
    {
        private readonly int _indent;
        private readonly StringBuilder _builder;

        public WriteListBuilder(int indent)
        {
            _indent = indent;
            _builder = new StringBuilder();
        }

        public void OnProperty(PropertyVisitorContext context, IPropertySymbol symbol, string prettyPropType)
        {
            if (_builder.Length > 0)
            {
                _builder.AppendLine();
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
                    _builder.AppendFormat("fields.Add({0}.ToString());", symbol.Name);
                    break;
                case "bool":
                case "bool?":
                    _builder.AppendFormat("fields.Add(CsvUtility.FormatBool({0}));", symbol.Name);
                    break;
                case "System.DateTimeOffset":
                case "System.DateTimeOffset?":
                    _builder.AppendFormat("fields.Add(CsvUtility.FormatDateTimeOffset({0}));", symbol.Name);
                    break;
                case "string":
                    _builder.AppendFormat("fields.Add({0});", symbol.Name);
                    break;
                default:
                    if (symbol.Type.TypeKind == TypeKind.Enum || PropertyHelper.IsNullableEnum(context, symbol))
                    {
                        _builder.AppendFormat("fields.Add({0}.ToString());", symbol.Name);
                    }
                    else
                    {
                        _builder.AppendFormat("fields.Add({0}?.ToString());", symbol.Name);
                    }
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
