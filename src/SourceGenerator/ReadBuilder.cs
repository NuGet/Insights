// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public class ReadBuilder : IPropertyVisitor
    {
        private readonly StringBuilder _builder;
        private readonly int _indent;

        public ReadBuilder(int indent)
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

            var propertyType = symbol.Type.ToString();
            switch (propertyType)
            {
                case "bool":
                case "ushort":
                case "short":
                case "uint":
                case "int":
                case "float":
                case "double":
                case "long":
                case "System.Guid":
                case "System.TimeSpan":
                    _builder.AppendFormat("{0} = {1}.Parse(getNextField()),", symbol.Name, prettyPropType);
                    break;
                case "bool?":
                case "ushort?":
                case "short?":
                case "uint?":
                case "int?":
                case "float?":
                case "double?":
                case "long?":
                case "System.Guid?":
                case "System.TimeSpan?":
                    _builder.AppendFormat("{0} = CsvUtility.ParseNullable(getNextField(), {1}.Parse),", symbol.Name, prettyPropType);
                    break;
                case "System.Version":
                    _builder.AppendFormat("{0} = CsvUtility.ParseReference(getNextField(), {1}.Parse),", symbol.Name, prettyPropType);
                    break;
                case "string":
                    _builder.AppendFormat("{0} = getNextField(),", symbol.Name);
                    break;
                case "System.DateTimeOffset":
                    _builder.AppendFormat("{0} = CsvUtility.ParseDateTimeOffset(getNextField()),", symbol.Name);
                    break;
                case "System.DateTimeOffset?":
                    _builder.AppendFormat("{0} = CsvUtility.ParseNullable(getNextField(), CsvUtility.ParseDateTimeOffset),", symbol.Name);
                    break;
                default:
                    if (symbol.Type.TypeKind == TypeKind.Enum)
                    {
                        _builder.AppendFormat("{0} = Enum.Parse<{1}>(getNextField()),", symbol.Name, prettyPropType);
                    }
                    else if (PropertyHelper.IsNullableEnum(context, symbol))
                    {
                        _builder.AppendFormat("{0} = CsvUtility.ParseNullable(getNextField(), Enum.Parse<{1}>),", symbol.Name, prettyPropType);
                    }
                    else
                    {
                        _builder.AppendFormat("{0} = Parse{0}(getNextField()),", symbol.Name);
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
