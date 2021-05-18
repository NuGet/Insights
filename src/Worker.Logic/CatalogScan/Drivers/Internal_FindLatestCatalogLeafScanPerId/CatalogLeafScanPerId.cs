// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.FindLatestCatalogLeafScanPerId
{
    public class CatalogLeafScanPerId : CatalogLeafScan
    {
        public CatalogLeafScanPerId()
        {
        }

        public CatalogLeafScanPerId(string storageSuffix, string scanId, string pageId, string leafId)
            : base(storageSuffix, scanId, pageId, leafId)
        {
        }
    }
}
