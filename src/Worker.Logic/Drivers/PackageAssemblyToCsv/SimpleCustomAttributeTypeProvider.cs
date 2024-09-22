// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection.Metadata;

namespace NuGet.Insights.Worker.PackageAssemblyToCsv
{
    public class SimpleCustomAttributeTypeProvider : ICustomAttributeTypeProvider<SimpleTypeInfo>
    {
        public static SimpleCustomAttributeTypeProvider Instance { get; } = new();

        public SimpleTypeInfo GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return SimpleTypeInfo.NewPrimitiveType(typeCode);
        }

        public SimpleTypeInfo GetSystemType()
        {
            return SimpleTypeInfo.NewRecognizedType(typeof(Type));
        }

        public SimpleTypeInfo GetSZArrayType(SimpleTypeInfo elementType)
        {
            return SimpleTypeInfo.NewArrayType(elementType);
        }

        public SimpleTypeInfo GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            return GetTypeInfoFromHandle(reader, handle);
        }

        public SimpleTypeInfo GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            return GetTypeInfoFromHandle(reader, handle);
        }

        public SimpleTypeInfo GetTypeFromSerializedName(string name)
        {
            return SimpleTypeInfo.NewSerializedName(name);
        }

        public PrimitiveTypeCode GetUnderlyingEnumType(SimpleTypeInfo type)
        {
            if (type.RecognizedType is null)
            {
                // If the type is not recognized (a 3rd party enum), assume the enum is an int (the default).
                // This can cause decoding to fail, for example by not progressing the attribute byte reader
                // far enough far enough causing other reads to be aligned to the wrong bytes. It's not possible
                // to know a 3rd party enum size without loading the correct dependency assembly, which is not
                // tenable in this context.
                type.IsUnrecognizedEnum = true;
                return PrimitiveTypeCode.Int32;
            }

            if (!type.RecognizedType.IsEnum)
            {
                throw new InvalidOperationException("The provided type is not an enum.");
            }

            var underlyingType = Enum.GetUnderlyingType(type.RecognizedType);

            return underlyingType switch
            {
                var t when t == typeof(sbyte) => PrimitiveTypeCode.SByte,
                var t when t == typeof(byte) => PrimitiveTypeCode.Byte,
                var t when t == typeof(short) => PrimitiveTypeCode.Int16,
                var t when t == typeof(ushort) => PrimitiveTypeCode.UInt16,
                var t when t == typeof(int) => PrimitiveTypeCode.Int32,
                var t when t == typeof(uint) => PrimitiveTypeCode.UInt32,
                var t when t == typeof(long) => PrimitiveTypeCode.Int64,
                var t when t == typeof(ulong) => PrimitiveTypeCode.UInt64,
                _ => throw new InvalidOperationException("Unexpected enum underlying type: " + underlyingType.FullName)
            };
        }

        public bool IsSystemType(SimpleTypeInfo type)
        {
            return type.RecognizedType == typeof(Type);
        }

        private static SimpleTypeInfo GetTypeInfoFromHandle(MetadataReader metadata, EntityHandle handle)
        {
            var entityInfo = GetEntityHandleInfo(metadata, handle);
            var fullTypeName = entityInfo.GetFullTypeName();

            var type = Type.GetType(fullTypeName, throwOnError: false);
            if (type is not null)
            {
                return SimpleTypeInfo.NewRecognizedType(type);
            }

            return SimpleTypeInfo.NewUnrecognizedType(entityInfo);
        }

        private static EntityHandleInfo GetEntityHandleInfo(MetadataReader metadata, EntityHandle handle)
        {
            switch (handle.Kind)
            {
                case HandleKind.AssemblyReference:
                    var assemblyReference = metadata.GetAssemblyReference((AssemblyReferenceHandle)handle);
                    return new EntityHandleInfo(
                        handle.Kind,
                        Namespace: null,
                        Name: null,
                        Assembly: assemblyReference.GetAssemblyName(),
                        Scope: null);

                case HandleKind.TypeReference:
                    var typeReference = metadata.GetTypeReference((TypeReferenceHandle)handle);
                    var scope = GetEntityHandleInfo(metadata, typeReference.ResolutionScope);
                    return new EntityHandleInfo(
                        HandleKind.TypeReference,
                        metadata.GetString(typeReference.Namespace),
                        metadata.GetString(typeReference.Name),
                        Assembly: null,
                        Scope: scope);

                default:
                    return new EntityHandleInfo(
                        handle.Kind,
                        Namespace: null,
                        Name: null,
                        Assembly: null,
                        Scope: null);
            }
        }
    }
}
