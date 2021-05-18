// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageConsistencyContext
    {
        public PackageConsistencyContext(
            string id,
            string version,
            bool isDeleted,
            bool isSemVer2,
            bool isListed,
            bool hasIcon)
        {
            Id = id;
            Version = version;
            IsDeleted = isDeleted;
            IsSemVer2 = isSemVer2;
            IsListed = isListed;
            HasIcon = hasIcon;
        }

        public string Id { get; }
        public string Version { get; }
        public bool IsDeleted { get; }
        public bool IsListed { get; }
        public bool HasIcon { get; }
        public bool IsSemVer2 { get; }
    }
}
