﻿// <auto-generated/>

#nullable enable annotations
#nullable disable warnings

namespace PolyType.SourceGenerator
{
    internal partial class ShapeProvider
    {
        /// <summary>Gets a generated shape for the specified type.</summary>
#nullable disable annotations // Use nullable-oblivious property type
        public global::PolyType.Abstractions.ITypeShape<long> Int64 => __Int64 ??= __Create_Int64();
#nullable enable annotations // Use nullable-oblivious property type
        private global::PolyType.Abstractions.ITypeShape<long>? __Int64;

        private global::PolyType.Abstractions.ITypeShape<long> __Create_Int64()
        {
            return new global::PolyType.SourceGenModel.SourceGenObjectTypeShape<long>
            {
                Provider = this,
                IsRecordType = false,
                IsTupleType = false,
            };
        }
    }
}