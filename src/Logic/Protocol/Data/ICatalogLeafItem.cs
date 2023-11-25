// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public interface ICatalogLeafItem
    {
        public string PackageId { get; }
        public string PackageVersion { get; }
        CatalogLeafType LeafType { get; }
        public DateTimeOffset CommitTimestamp { get; }
        string Url { get; }
        string CommitId { get; }
    }
}
