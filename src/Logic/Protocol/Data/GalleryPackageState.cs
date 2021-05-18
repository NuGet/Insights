// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class GalleryPackageState
    {
        public GalleryPackageState(
            string packageId,
            string packageVersion,
            PackageDeletedStatus packageDeletedStatus,
            bool isListed,
            bool hasIcon)
        {
            PackageId = packageId;
            PackageVersion = packageVersion;
            PackageDeletedStatus = packageDeletedStatus;
            IsListed = isListed;
            HasIcon = hasIcon;
        }

        public string PackageId { get; }
        public string PackageVersion { get; }
        public PackageDeletedStatus PackageDeletedStatus { get; }
        public bool IsListed { get; }
        public bool HasIcon { get; }
    }
}
