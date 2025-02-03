// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        public void OnProperty(SourceProductionContext context, CsvRecordModel model, CsvPropertyModel property)
        {
            if (_builder.Length > 0)
            {
                _builder.AppendLine();
            }

            _builder.Append(' ', _indent);

            switch (property.Type)
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
                    _builder.AppendFormat("fields.Add({0}.ToString());", property.Name);
                    break;
                case "bool":
                case "bool?":
                    _builder.AppendFormat("fields.Add(CsvUtility.FormatBool({0}));", property.Name);
                    break;
                case "System.DateTimeOffset":
                case "System.DateTimeOffset?":
                    _builder.AppendFormat("fields.Add(CsvUtility.FormatDateTimeOffset({0}));", property.Name);
                    break;
                case "string":
                    _builder.AppendFormat("fields.Add({0});", property.Name);
                    break;
                default:
                    if (property.IsEnum || property.IsNullableEnum)
                    {
                        _builder.AppendFormat("fields.Add({0}.ToString());", property.Name);
                    }
                    else
                    {
                        _builder.AppendFormat("fields.Add({0}?.ToString());", property.Name);
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
