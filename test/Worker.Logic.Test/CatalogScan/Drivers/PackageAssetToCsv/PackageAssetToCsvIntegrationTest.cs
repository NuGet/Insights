// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageAssetToCsv
{
    public class PackageAssetToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageAsset>
    {
        private const string PackageAssetToCsvDir = nameof(PackageAssetToCsv);
        private const string PackageAssetToCsv_WithDeleteDir = nameof(PackageAssetToCsv_WithDelete);
        private const string PackageAssetToCsv_WithDuplicatesDir = nameof(PackageAssetToCsv_WithDuplicates);

        [Fact]
        public async Task PackageAssetToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssetToCsvDir, Step1, 0);
            await AssertOutputAsync(PackageAssetToCsvDir, Step1, 1);
            await AssertOutputAsync(PackageAssetToCsvDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageAssetToCsvDir, Step2, 0);
            await AssertOutputAsync(PackageAssetToCsvDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageAssetToCsvDir, Step2, 2);
        }

        [Fact]
        public async Task PackageAssetToCsv_WithoutBatching()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AllowBatching = false;

            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssetToCsvDir, Step1, 0);
            await AssertOutputAsync(PackageAssetToCsvDir, Step1, 1);
            await AssertOutputAsync(PackageAssetToCsvDir, Step1, 2);
        }

        [Theory]
        [InlineData("00:01:00", true, false)]
        [InlineData("00:01:00", false, false)]
        [InlineData("00:02:00", true, true)]
        [InlineData("00:02:00", false, true)]
        public async Task PackageAssetToCsv_LeafLevelTelemetry(string threshold, bool onlyLatestLeaves, bool expectLogs)
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2016-07-28T16:12:06.0020479Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2016-07-28T16:13:37.3231638Z", CultureInfo.InvariantCulture);

            ConfigureWorkerSettings = x =>
            {
                x.AllowBatching = false;
                x.LeafLevelTelemetryThreshold = TimeSpan.Parse(threshold, CultureInfo.InvariantCulture);
            };

            if (!onlyLatestLeaves)
            {
                MutableLatestLeavesTypes.Clear();
            }

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(DriverType, onlyLatestLeaves, max1);

            // Assert
            if (expectLogs)
            {
                Assert.Contains(LogMessages, x => x.Contains("Metric emitted: CatalogScanExpandService.EnqueueLeafScansAsync.CatalogLeafScan = 1", StringComparison.Ordinal));
                Assert.Contains(LogMessages, x => x.Contains("Metric emitted: CatalogLeafScanMessageProcessor.ToProcess.CatalogLeafScan = 1", StringComparison.Ordinal));
                Assert.Contains(LogMessages, x => x.Contains("Metric emitted: CatalogScanStorageService.DeleteAsync.Single.CatalogLeafScan = 1", StringComparison.Ordinal));
            }
            else
            {
                Assert.DoesNotContain(LogMessages, x => new Regex("Metric emitted: (.+?)\\.CatalogLeafScan = ").IsMatch(x));
            }
        }

        [Fact]
        public async Task PackageAssetToCsv_WithDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step1, 0);
            await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step1, 1);
            await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
            await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageAssetToCsv_WithDeleteDir, Step2, 2);
        }

        [Fact]
        public Task PackageAssetToCsv_WithDuplicates_OnlyLatestLeaves()
        {
            return PackageAssetToCsv_WithDuplicates(batchProcessing: false);
        }

        [Fact]
        public async Task PackageAssetToCsv_WithDuplicates_AllLeaves()
        {
            MutableLatestLeavesTypes.Clear();
            await PackageAssetToCsv_WithDuplicates(batchProcessing: false);
        }

        [Fact]
        public Task PackageAssetToCsv_WithDuplicates_BatchProcessing()
        {
            MutableLatestLeavesTypes.Clear();
            return PackageAssetToCsv_WithDuplicates(batchProcessing: true);
        }

        [Fact]
        public Task PackageAssetToCsv_WithDuplicates_OnlyLatestLeaves_FailedRangeRequests()
        {
            FailRangeRequests();
            return PackageAssetToCsv_WithDuplicates(batchProcessing: false);
        }

        [Fact]
        public async Task PackageAssetToCsv_WithDuplicates_AllLeaves_FailedRangeRequests()
        {
            MutableLatestLeavesTypes.Clear();
            FailRangeRequests();
            await PackageAssetToCsv_WithDuplicates(batchProcessing: false);
        }

        [Fact]
        public Task PackageAssetToCsv_WithDuplicates_BatchProcessing_FailedRangeRequests()
        {
            MutableLatestLeavesTypes.Clear();
            FailRangeRequests();
            return PackageAssetToCsv_WithDuplicates(batchProcessing: true);
        }

        public PackageAssetToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
            MutableLatestLeavesTypes.Add(DriverType);
        }

        protected override string DestinationContainerName => Options.Value.PackageAssetContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageAssetToCsv;

        private List<CatalogScanDriverType> MutableLatestLeavesTypes { get; } = new();

        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => MutableLatestLeavesTypes;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        private async Task PackageAssetToCsv_WithDuplicates(bool batchProcessing)
        {
            ConfigureWorkerSettings = x =>
            {
                x.AppendResultStorageBucketCount = 1;
                x.RunAllCatalogScanDriversAsBatch = batchProcessing;
            };

            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssetToCsv_WithDuplicatesDir, Step1, 0);

            var duplicatePackageRequests = HttpMessageHandlerFactory
                .SuccessRequests
                .Where(x => x.RequestUri.GetLeftPart(UriPartial.Path).EndsWith("/gosms.ge-sms-api.1.0.1.nupkg", StringComparison.Ordinal))
                .ToList();
            var onlyLatestLeaves = LatestLeavesTypes.Contains(DriverType);
            Assert.Equal(onlyLatestLeaves ? 1 : 2, duplicatePackageRequests.Count(x => x.Method == HttpMethod.Get));
        }

        private void FailRangeRequests()
        {
            HttpMessageHandlerFactory.OnSendAsync = (req, b, t) =>
            {
                if (req.Method == HttpMethod.Get && req.Headers.Range is not null)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        RequestMessage = req,
                    });
                }

                return Task.FromResult<HttpResponseMessage>(null);
            };
        }

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[] { "CatalogScan-" + CatalogScanDriverType.LoadPackageArchive });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageArchiveTableName });
        }
    }
}
