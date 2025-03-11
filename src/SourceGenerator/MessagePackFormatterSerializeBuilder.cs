// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public class MessagePackFormatterSerializeBuilder : IPropertyVisitor
    {
        private readonly StringBuilder _builder;
        private readonly int _indent;

        public MessagePackFormatterSerializeBuilder(int indent)
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
                case "byte":
                case "char":
                case "double":
                case "float":
                case "int":
                case "long":
                case "sbyte":
                case "string":
                case "short":
                case "uint":
                case "ulong":
                case "ushort":
                    _builder.AppendFormat("writer.Write(value.{0});", property.Name);
                    break;
                default:
                    if (property.IsEnum)
                    {
                        _builder.AppendFormat("writer.Write(({0})value.{1});", property.UnderlyingEnumType, property.Name);
                    }
                    else
                    {
                        _builder.AppendFormat("MessagePackSerializer.Serialize(ref writer, value.{0}, options);", property.Name);
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
