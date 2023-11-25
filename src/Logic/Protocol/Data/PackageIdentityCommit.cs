// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageIdentityCommit : IPackageIdentityCommit
    {
        public required string PackageId { get; set; }
        public required string PackageVersion { get; set; }
        public required CatalogLeafType LeafType { get; set; }
        public required DateTimeOffset? CommitTimestamp { get; set; }
    }
}
