// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class CatalogScanServiceTest
    {
        private partial class DriverInfo
        {
            public static DriverInfo NuGetPackageExplorerToCsv => new DriverInfo
            {
                DefaultMin = CatalogClient.NuGetOrgMinDeleted,
                OnlyLatestLeavesSupport = null,
                SupportsBucketRangeProcessing = true,
                SetDependencyCursorAsync = (self, x) =>
                {
                    self.FlatContainerCursor = x;
                    return Task.CompletedTask;
                },
            };
        }
    }
}