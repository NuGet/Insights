// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class CatalogScanServiceIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public DateTimeOffset Min { get; }
        public DateTimeOffset Max { get; }
        public CatalogScanDriverType DriverType { get; }
        public string ScanId { get; }
        public string StorageSuffix { get; }
        public int[] Buckets { get; }

        [Fact]
        public async Task Sandbox()
        {
            // Arrange
            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, Min);
            await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, Max);

            // Act
            var result = await CatalogScanService.UpdateAsync(ScanId, StorageSuffix, DriverType, Buckets);
            await UpdateAsync(result.Scan);

            // Assert
        }

        public CatalogScanServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            Min = DateTimeOffset.Parse("2023-11-11T17:52:25.2168748Z");
            Max = DateTimeOffset.Parse("2023-11-11T17:53:29.3145280Z");
            DriverType = CatalogScanDriverType.PackageAssetToCsv;
            ScanId = "my-scan-id";
            StorageSuffix = "zz";
            Buckets = new[] { 331, 435, 571, 572, 630, 631, 632, 633, 634 };
        }
    }
}
