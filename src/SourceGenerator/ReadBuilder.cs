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

        public void OnProperty(SourceProductionContext context, CsvRecordModel model, CsvPropertyModel property)
        {
            if (_builder.Length > 0)
            {
                _builder.AppendLine();
            }

            _builder.Append(' ', _indent);

            switch (property.Type)
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
                    _builder.AppendFormat("{0} = {1}.Parse(getNextField()),", property.Name, property.PrettyType);
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
                    _builder.AppendFormat("{0} = CsvUtility.ParseNullable(getNextField(), {1}.Parse),", property.Name, property.PrettyType);
                    break;
                case "System.Version":
                    _builder.AppendFormat("{0} = CsvUtility.ParseReference(getNextField(), {1}.Parse),", property.Name, property.PrettyType);
                    break;
                case "string":
                    _builder.AppendFormat("{0} = getNextField(),", property.Name);
                    break;
                case "System.DateTimeOffset":
                    _builder.AppendFormat("{0} = CsvUtility.ParseDateTimeOffset(getNextField()),", property.Name);
                    break;
                case "System.DateTimeOffset?":
                    _builder.AppendFormat("{0} = CsvUtility.ParseNullable(getNextField(), CsvUtility.ParseDateTimeOffset),", property.Name);
                    break;
                default:
                    if (property.IsEnum)
                    {
                        _builder.AppendFormat("{0} = Enum.Parse<{1}>(getNextField()),", property.Name, property.PrettyType);
                    }
                    else if (property.IsNullableEnum)
                    {
                        _builder.AppendFormat("{0} = CsvUtility.ParseNullable(getNextField(), Enum.Parse<{1}>),", property.Name, property.PrettyType);
                    }
                    else
                    {
                        _builder.AppendFormat("{0} = Parse{0}(getNextField()),", property.Name);
                    }
                    break;
            }
        }

        public void Finish(SourceProductionContext context, CsvRecordModel model)
        {
        }

        public string GetResult()
        {
            return _builder.ToString();
        }
    }
}
