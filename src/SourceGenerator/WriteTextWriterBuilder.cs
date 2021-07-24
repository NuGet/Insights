// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using Microsoft.CodeAnalysis;

namespace NuGet.Insights
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

        public void OnProperty(PropertyVisitorContext context, IPropertySymbol symbol, string prettyPropType)
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
                case "ushort":
                case "ushort?":
                case "uint":
                case "uint?":
                case "int":
                case "int?":
                case "float":
                case "float?":
                case "double":
                case "double?":
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
                    if (symbol.Type.TypeKind == TypeKind.Enum || PropertyHelper.IsNullableEnum(context, symbol))
                    {
                        _builder.AppendFormat("CsvUtility.WriteWithQuotes(writer, {0}.ToString());", symbol.Name);
                    }
                    else
                    {
                        _builder.AppendFormat("CsvUtility.WriteWithQuotes(writer, {0}?.ToString());", symbol.Name);
                    }
                    break;
            }
        }

        public void Finish(PropertyVisitorContext context)
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
