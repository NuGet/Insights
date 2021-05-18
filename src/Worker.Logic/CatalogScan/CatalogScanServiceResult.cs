// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CatalogScanServiceResult
    {
        public CatalogScanServiceResult(CatalogScanServiceResultType type, string dependencyName, CatalogIndexScan scan)
        {
            Type = type;
            DependencyName = dependencyName;
            Scan = scan;
        }

        public CatalogScanServiceResultType Type { get; }
        public string DependencyName { get; }
        public CatalogIndexScan Scan { get; }
    }
}
