// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class FlatContainerConsistencyReport : IConsistencyReport
    {
        public FlatContainerConsistencyReport(
           bool isConsistent,
           BlobMetadata packageContentMetadata,
           bool hasPackageManifest,
           bool hasIcon,
           bool isInIndex)
        {
            IsConsistent = isConsistent;
            PackageContentMetadata = packageContentMetadata;
            HasPackageManifest = hasPackageManifest;
            HasPackageIcon = hasIcon;
            IsInIndex = isInIndex;
        }

        public bool IsConsistent { get; }
        public BlobMetadata PackageContentMetadata { get; }
        public bool HasPackageManifest { get; }
        public bool HasPackageIcon { get; }
        public bool IsInIndex { get; }
    }
}
