// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial struct CatalogScanDriverType
    {
        /// <summary>
        /// Implemented by <see cref="FindLatestCatalogLeafScan.LatestCatalogLeafScanStorageFactory"/> and <see cref="FindLatestLeafDriver{T}"/>.
        /// This is a helper catalog scan used by <see cref="CatalogIndexScanMessageProcessor"/> when the driver returns
        /// <see cref="CatalogIndexScanResult.ExpandLatestLeaves"/>. This allows another driver to only process the latest
        /// catalog leaf per version instead of duplicating effort which is inevitable in the NuGet.org catalog.
        /// </summary>
        public static CatalogScanDriverType Internal_FindLatestCatalogLeafScan { get; } = new CatalogScanDriverType(nameof(Internal_FindLatestCatalogLeafScan));

        /// <summary>
        /// Implemented by <see cref="FindLatestCatalogLeafScanPerId.LatestCatalogLeafScanPerIdStorageFactory"/> and <see cref="FindLatestLeafDriver{T}"/>.
        /// This is a helper catalog scan used by <see cref="CatalogIndexScanMessageProcessor"/> when the driver returns
        /// <see cref="CatalogIndexScanResult.ExpandLatestLeavesPerId"/>. This allows another driver to only process the latest
        /// catalog leaf per ID instead of duplicating effort which is inevitable in the NuGet.org catalog.
        /// </summary>
        public static CatalogScanDriverType Internal_FindLatestCatalogLeafScanPerId { get; } = new CatalogScanDriverType(nameof(Internal_FindLatestCatalogLeafScanPerId));
    }
}
