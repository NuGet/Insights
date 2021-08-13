// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights
{
    public interface ICatalogLeafItem
    {
        string Url { get; }
        string CommitId { get; }
        DateTimeOffset CommitTimestamp { get; }
        string PackageId { get; }
        string PackageVersion { get; }
        CatalogLeafType Type { get; }
    }
}
