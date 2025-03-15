// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace NuGet.Insights
{
    public class MessagePackFormatterDeserializeBuilder : IPropertyVisitor
    {
        private readonly StringBuilder _builder;
        private readonly int _indent;

        public MessagePackFormatterDeserializeBuilder(int indent)
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
                case "sbyte":
                    _builder.AppendFormat("record.{0} = reader.ReadSByte();", property.Name);
                    break;
                case "short":
                    _builder.AppendFormat("record.{0} = reader.ReadInt16();", property.Name);
                    break;
                case "int":
                    _builder.AppendFormat("record.{0} = reader.ReadInt32();", property.Name);
                    break;
                case "long":
                    _builder.AppendFormat("record.{0} = reader.ReadInt64();", property.Name);
                    break;
                case "byte":
                    _builder.AppendFormat("record.{0} = reader.ReadByte();", property.Name);
                    break;
                case "ushort":
                    _builder.AppendFormat("record.{0} = reader.ReadUInt16();", property.Name);
                    break;
                case "uint":
                    _builder.AppendFormat("record.{0} = reader.ReadUInt32();", property.Name);
                    break;
                case "ulong":
                    _builder.AppendFormat("record.{0} = reader.ReadUInt64();", property.Name);
                    break;
                case "double":
                    _builder.AppendFormat("record.{0} = reader.ReadDouble();", property.Name);
                    break;
                case "float":
                    _builder.AppendFormat("record.{0} = reader.ReadFloat();", property.Name);
                    break;
                case "char":
                    _builder.AppendFormat("record.{0} = reader.ReadChar();", property.Name);
                    break;
                case "bool":
                    _builder.AppendFormat("record.{0} = reader.ReadBoolean();", property.Name);
                    break;
                case "string":
                    _builder.AppendFormat("record.{0} = reader.ReadString();", property.Name);
                    break;
                default:
                    if (property.IsEnum)
                    {
                        switch (property.UnderlyingEnumType!)
                        {
                            case "sbyte":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadSByte();", property.Name, property.Type);
                                break;
                            case "short":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadInt16();", property.Name, property.Type);
                                break;
                            case "int":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadInt32();", property.Name, property.Type);
                                break;
                            case "long":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadInt64();", property.Name, property.Type);
                                break;
                            case "byte":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadByte();", property.Name, property.Type);
                                break;
                            case "ushort":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadUInt16();", property.Name, property.Type);
                                break;
                            case "uint":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadUInt32();", property.Name, property.Type);
                                break;
                            case "ulong":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadUInt64();", property.Name, property.Type);
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                    }
                    else
                    {
                        _builder.AppendFormat("record.{0} = MessagePackSerializer.Deserialize<{1}>(ref reader, options);", property.Name, property.Type);
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
