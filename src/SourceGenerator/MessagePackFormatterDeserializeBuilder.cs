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
                case "sbyte":
                    _builder.AppendFormat("record.{0} = reader.ReadSByte();", symbol.Name);
                    break;
                case "short":
                    _builder.AppendFormat("record.{0} = reader.ReadInt16();", symbol.Name);
                    break;
                case "int":
                    _builder.AppendFormat("record.{0} = reader.ReadInt32();", symbol.Name);
                    break;
                case "long":
                    _builder.AppendFormat("record.{0} = reader.ReadInt64();", symbol.Name);
                    break;
                case "byte":
                    _builder.AppendFormat("record.{0} = reader.ReadByte();", symbol.Name);
                    break;
                case "ushort":
                    _builder.AppendFormat("record.{0} = reader.ReadUInt16();", symbol.Name);
                    break;
                case "uint":
                    _builder.AppendFormat("record.{0} = reader.ReadUInt32();", symbol.Name);
                    break;
                case "ulong":
                    _builder.AppendFormat("record.{0} = reader.ReadUInt64();", symbol.Name);
                    break;
                case "double":
                    _builder.AppendFormat("record.{0} = reader.ReadDouble();", symbol.Name);
                    break;
                case "float":
                    _builder.AppendFormat("record.{0} = reader.ReadFloat();", symbol.Name);
                    break;
                case "char":
                    _builder.AppendFormat("record.{0} = reader.ReadChar();", symbol.Name);
                    break;
                case "bool":
                    _builder.AppendFormat("record.{0} = reader.ReadBoolean();", symbol.Name);
                    break;
                case "string":
                    _builder.AppendFormat("record.{0} = reader.ReadString();", symbol.Name);
                    break;
                default:
                    if (symbol.Type.TypeKind == TypeKind.Enum
                        && symbol.Type is INamedTypeSymbol namedTypeSymbol)
                    {
                        switch (namedTypeSymbol.EnumUnderlyingType.ToString())
                        {
                            case "sbyte":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadSByte();", symbol.Name, propertyType);
                                break;
                            case "short":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadInt16();", symbol.Name, propertyType);
                                break;
                            case "int":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadInt32();", symbol.Name, propertyType);
                                break;
                            case "long":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadInt64();", symbol.Name, propertyType);
                                break;
                            case "byte":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadByte();", symbol.Name, propertyType);
                                break;
                            case "ushort":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadUInt16();", symbol.Name, propertyType);
                                break;
                            case "uint":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadUInt32();", symbol.Name, propertyType);
                                break;
                            case "ulong":
                                _builder.AppendFormat("record.{0} = ({1})reader.ReadUInt64();", symbol.Name, propertyType);
                                break;
                            default:
                                throw new NotSupportedException();
                        }
                    }
                    else
                    {
                        _builder.AppendFormat("record.{0} = MessagePackSerializer.Deserialize<{1}>(ref reader, options);", symbol.Name, propertyType);
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
