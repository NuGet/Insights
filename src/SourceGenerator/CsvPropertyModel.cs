// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

#nullable enable

namespace NuGet.Insights
{
    public record CsvPropertyModel(
        string FullName,
        string Name,
        string Type,
        string PrettyType,
        ImmutableArray<Location> Locations,
        bool IsNullable,
        bool IsNullableEnum,
        bool IsEnum,
        string? UnderlyingEnumType,
        bool IsReferenceType,
        bool IsBucketKey,
        bool IsKustoIgnore,
        bool IsRequired,
        bool IsKustoPartitionKey,
        string? KustoType);
}
