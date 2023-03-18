// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Insights.Worker.LoadBucketedPackage;
using NuGet.Insights.Worker.PackageSignatureToCsv;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class CatalogScanServiceIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        private const string CatalogScanService_UpdateWithBucketsDir = nameof(CatalogScanService_UpdateWithBuckets);

        [Fact]
        public async Task CatalogScanService_UpdateWithBuckets()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadBucketedPackage, Min);
            await UpdateAsync(CatalogScanDriverType.LoadBucketedPackage, Max);
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, Max);
            var cursorBefore = await SetCursorAsync(DriverType, Max);

            // Act
            var result = await CatalogScanService.UpdateAsync(ScanId, StorageSuffix, DriverType, Buckets);
            Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
            await UpdateAsync(result.Scan);

            // Assert
            Assert.Equal(CatalogScanServiceResultType.NewStarted, result.Type);
            Assert.Equal(ScanId, result.Scan.ScanId);
            Assert.Equal(StorageSuffix, result.Scan.StorageSuffix);
            var scan = await CatalogScanStorageService.GetIndexScanAsync(DriverType, ScanId);
            Assert.Equal(CatalogIndexScanState.Complete, scan.State);
            var cursorAfter = await CatalogScanCursorService.GetCursorAsync(DriverType);
            Assert.Equal(Max, cursorAfter.Value);
            Assert.Equal(cursorBefore.ETag, cursorAfter.ETag);

            var csvContent = await AssertPackageSignatureOutputAsync(CatalogScanService_UpdateWithBucketsDir, Step1, 0);

            var bucketedPackages = (await GetEntitiesAsync<BucketedPackage>(Options.Value.BucketedPackageTableName))
                .Where(x => Buckets.Contains(x.GetBucket()))
                .OrderBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.PackageVersion, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var packageArchiveEntities = (await GetWideEntitiesAsync<PackageFileService.PackageFileInfoVersions>(Options.Value.PackageArchiveTableName))
                .OrderBy(x => x.PartitionKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.RowKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var csvLines = csvContent.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).Skip(1).ToList();

            Assert.Equal(7, bucketedPackages.Count);
            Assert.Equal(7, packageArchiveEntities.Count);
            Assert.Equal(7, csvLines.Count);
            Assert.All(bucketedPackages.Zip(packageArchiveEntities, csvLines), tuple =>
            {
                var (bp, pa, csvLine) = tuple;
                Assert.Equal(bp.PackageId.ToLowerInvariant(), pa.PartitionKey);
                Assert.Equal(bp.ParsePackageVersion().ToNormalizedString().ToLowerInvariant(), pa.RowKey);
                Assert.StartsWith($",,{pa.PartitionKey},{pa.PartitionKey}/{pa.RowKey},", csvLine, StringComparison.Ordinal);
            });
        }

        public DateTimeOffset Min { get; }
        public DateTimeOffset Max { get; }
        public CatalogScanDriverType DriverType { get; }
        public string ScanId { get; }
        public string StorageSuffix { get; }
        public int[] Buckets { get; }

        protected async Task<string> AssertPackageSignatureOutputAsync(string testName, string stepName, int bucket)
        {
            return await AssertCompactAsync<PackageSignature>(Options.Value.PackageSignatureContainerName, testName, stepName, bucket);
        }

        public CatalogScanServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            Min = DateTimeOffset.Parse("2018-12-06T03:17:32.1388561Z", CultureInfo.InvariantCulture);
            Max = DateTimeOffset.Parse("2018-12-06T03:17:41.9986142Z", CultureInfo.InvariantCulture);
            DriverType = CatalogScanDriverType.PackageSignatureToCsv;
            ScanId = "my-scan-id";
            StorageSuffix = "zz";
            Buckets = new[] { 375, 401, 826, 827, 828, 829 };
        }
    }
}
