// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

#nullable enable

namespace NuGet.Insights
{
    public record CsvRecordModel(
        string? AssemblyName,
        string? KustoDDLName,
        string Namespace,
        string TypeKeyword,
        string Name,
        ImmutableArray<Location> Locations,
        EquatableList<CsvPropertyModel> Properties);
}
