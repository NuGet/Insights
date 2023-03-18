// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class CatalogScanServiceIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public CatalogScanDriverType DriverType { get; }
        public string ScanId { get; }
        public string StorageSuffix { get; }
        public int[] Buckets { get; }

        public async Task Sandbox()
        {
            // Arrange
            // SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, )
            // CatalogScanService.UpdateAsync()
        }

        public CatalogScanServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            DriverType = CatalogScanDriverType.PackageAssetToCsv;
            ScanId = "my-scan-id";
            StorageSuffix = "zz";
            Buckets = new[] { 23, 24, 42, 25 };
        }
    }
}
