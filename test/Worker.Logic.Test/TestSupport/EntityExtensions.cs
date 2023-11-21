// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public static class EntityExtensions
    {
        public static CatalogIndexScan SetDefaults(this CatalogIndexScan scan)
        {
            if (scan.Min < StorageUtility.MinTimestamp)
            {
                scan.Min = StorageUtility.MinTimestamp;
            }

            if (scan.Max < StorageUtility.MinTimestamp)
            {
                scan.Max = StorageUtility.MinTimestamp;
            }

            return scan;
        }

        public static CatalogPageScan SetDefaults(this CatalogPageScan scan)
        {
            if (scan.Min < StorageUtility.MinTimestamp)
            {
                scan.Min = StorageUtility.MinTimestamp;
            }

            if (scan.Max < StorageUtility.MinTimestamp)
            {
                scan.Max = StorageUtility.MinTimestamp;
            }

            return scan;
        }

        public static CatalogLeafScan SetDefaults(this CatalogLeafScan scan)
        {
            if (scan.LeafType == default)
            {
                scan.LeafType = CatalogLeafType.PackageDetails;
            }

            if (scan.Min < StorageUtility.MinTimestamp)
            {
                scan.Min = StorageUtility.MinTimestamp;
            }

            if (scan.Max < StorageUtility.MinTimestamp)
            {
                scan.Max = StorageUtility.MinTimestamp;
            }

            if (scan.CommitTimestamp < StorageUtility.MinTimestamp)
            {
                scan.CommitTimestamp = StorageUtility.MinTimestamp;
            }

            return scan;
        }
    }
}
