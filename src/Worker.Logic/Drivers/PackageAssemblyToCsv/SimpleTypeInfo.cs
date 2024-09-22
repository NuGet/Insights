// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection.Metadata;

namespace NuGet.Insights.Worker.PackageAssemblyToCsv
{
    [JsonConverter(typeof(SimpleTypeInfoJsonConverter))]
    public class SimpleTypeInfo
    {
        public static SimpleTypeInfo NewPrimitiveType(PrimitiveTypeCode primitiveTypeCode)
        {
            return new SimpleTypeInfo { PrimitiveTypeCode = primitiveTypeCode };
        }

        public static SimpleTypeInfo NewRecognizedType(Type recognizedType)
        {
            return new SimpleTypeInfo { RecognizedType = recognizedType };
        }

        public static SimpleTypeInfo NewSerializedName(string serializedName)
        {
            return new SimpleTypeInfo { SerializedName = serializedName };
        }

        public static SimpleTypeInfo NewArrayType(SimpleTypeInfo arrayType)
        {
            return new SimpleTypeInfo { ArrayType = arrayType };
        }

        public static SimpleTypeInfo NewUnrecognizedType(EntityHandleInfo handleInfo)
        {
            return new SimpleTypeInfo { UnrecognizedType = handleInfo };
        }

        public Type RecognizedType { get; private init; }
        public PrimitiveTypeCode? PrimitiveTypeCode { get; private init; }
        public string SerializedName { get; private init; }
        public SimpleTypeInfo ArrayType { get; private init; }
        public EntityHandleInfo UnrecognizedType { get; private init; }

        public bool IsUnrecognizedEnum { get; set; }

        public class SimpleTypeInfoJsonConverter : JsonConverter<SimpleTypeInfo>
        {
            public override SimpleTypeInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }

            private static string GetSerializableValue(SimpleTypeInfo value)
            {
                return value switch
                {
                    { RecognizedType: not null } => value.RecognizedType.FullName,
                    { PrimitiveTypeCode: not null } => value.PrimitiveTypeCode.Value.ToString(),
                    { SerializedName: not null } => value.SerializedName,
                    { ArrayType: not null } => GetSerializableValue(value.ArrayType) + "[]",
                    { UnrecognizedType: not null } => value.UnrecognizedType.GetFullTypeName(),
                    _ => throw new NotImplementedException(),
                };
            }

            public override void Write(Utf8JsonWriter writer, SimpleTypeInfo value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(GetSerializableValue(value));
            }
        }
    }
}
