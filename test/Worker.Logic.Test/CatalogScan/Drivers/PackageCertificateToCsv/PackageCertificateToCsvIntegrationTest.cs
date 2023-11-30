// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;
using NuGet.Insights.ReferenceTracking;
using NuGet.Insights.Worker.ReferenceTracking;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class PackageCertificateToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageCertificateRecord, CertificateRecord>
    {
        [Fact]
        public async Task Simple()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2021-12-09T22:05:17.4250722Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2021-12-09T22:05:41.2080967Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2021-12-09T22:06:08.5695122Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AddTargetsAsync(
                GetTableOutputAsync(step: 1),
                GetCsvOutputAsync(step: 1));

            // Act
            await UpdateAsync(max2);

            // Assert
            await AddTargetsAsync(
                GetTableOutputAsync(step: 2),
                GetCsvOutputAsync(step: 2));
            await VerifyAndClearAsync();
        }

        [Fact]
        public async Task ManyCertificates()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2023-01-17T09:51:59.7223256Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2023-01-17T09:52:47.3352455Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AddTargetsAsync(
                GetTableOutputAsync(step: 1),
                GetCsvOutputAsync(step: 1));
            await VerifyAndClearAsync();
        }

        [Fact]
        public async Task EVCodeSigningCertificate()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2022-01-25T12:38:47.2179093Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2022-01-25T12:39:12.000987Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AddTargetsAsync(
                GetTableOutputAsync(step: 1),
                GetCsvOutputAsync(step: 1));
            await VerifyAndClearAsync();
        }

        [Fact]
        public async Task Delete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            SetupCleanupOrphans();

            // Act
            await UpdateAsync(max1);

            // Assert
            await AddTargetsAsync(
                GetTableOutputAsync(step: 1),
                GetCsvOutputAsync(step: 1));

            // Act
            await UpdateAsync(max2);

            // Assert
            await AddTargetsAsync(
                GetOwnerToSubjectAsync(step: 1),
                GetSubjectToOwnerAsync(step: 1),
                GetCsvT1Async(bucket: 0, step: 1), // This file is unchanged.
                GetCsvT1Async(bucket: 1, step: 1), // This file is unchanged.
                GetCsvT1Async(bucket: 2, step: 2),
                GetCsvT2Async(bucket: 0, step: 1), // This file is unchanged.
                GetCsvT2Async(bucket: 1, step: 1), // This file is unchanged.
                GetCsvT2Async(bucket: 2, step: 1)); // This file is unchanged.

            // Act
            await CleanupOrphansAsync();

            // Assert
            await AddTargetsAsync(
                GetOwnerToSubjectAsync(step: 2), // This file is unchanged.
                GetSubjectToOwnerAsync(step: 3),
                GetCsvT1Async(bucket: 0, step: 1), // This file is unchanged.
                GetCsvT1Async(bucket: 1, step: 1), // This file is unchanged.
                GetCsvT1Async(bucket: 2, step: 2), // This file is unchanged.
                GetCsvT2Async(bucket: 0, step: 1), // This file is unchanged.
                GetCsvT2Async(bucket: 1, step: 3),
                GetCsvT2Async(bucket: 2, step: 1)); // This file is unchanged.
            await VerifyAndClearAsync();
        }

        [Fact]
        public async Task SingleDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable(id: "DeltaX", version: "1.0.0");
            var min0 = DateTimeOffset.Parse("2019-02-05T18:40:48.4041109Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2019-02-05T18:42:32.4259826Z", CultureInfo.InvariantCulture);
            var min2 = DateTimeOffset.Parse("2020-04-10T18:16:55.1000051Z", CultureInfo.InvariantCulture);
            var max3 = DateTimeOffset.Parse("2020-04-10T18:18:43.6788949Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max3);
            await SetCursorAsync(min0);

            SetupCleanupOrphans();

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertCsvCountT1Async(1);
            await AssertCsvCountT2Async(2);
            await AddTargetsAsync(
                GetOwnerToSubjectAsync(step: 1),
                GetSubjectToOwnerAsync(step: 1),
                GetCsvT1Async(bucket: 1, step: 1),
                GetCsvT2Async(bucket: 1, step: 1),
                GetCsvT2Async(bucket: 2, step: 1));

            // Act
            await SetCursorAsync(DriverType, min2);
            await UpdateAsync(max3);

            // Assert
            await AssertEmptyOwnerToSubjectAsync();
            await AssertCsvCountT1Async(1);
            await AssertCsvCountT2Async(2);
            await AddTargetsAsync(
                GetSubjectToOwnerAsync(step: 2),
                GetCsvT1Async(bucket: 1, step: 2),
                GetCsvT2Async(bucket: 1, step: 1), // This file is unchanged.
                GetCsvT2Async(bucket: 2, step: 1))) // This file is unchanged.
                .DisableRequireUniquePrefix();

            // Act
            await CleanupOrphansAsync();

            // Assert
            await AssertEmptyOwnerToSubjectAsync();
            await AssertEmptySubjectToOwnerAsync();
            await AssertCsvCountT1Async(1);
            await AssertEmptyCsvT2Async(bucket: 1);
            await AssertEmptyCsvT2Async(bucket: 2);
            await AssertCsvCountT2Async(2);
            await Verify(await GetCsvT1Async(bucket: 1, step: 2)).DisableRequireUniquePrefix(); // This file is unchanged.
        }

        [Fact]
        public async Task DollarSignId()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2016-03-20T22:08:18.0892014Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2016-03-21T06:41:26.9274378Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertEmptyOwnerToSubjectAsync();
            await AssertEmptySubjectToOwnerAsync();
            await AssertCsvCountT1Async(1);
            await AssertCsvCountT2Async(0);
            await Verify(await GetCsvT1Async(bucket: 2, step: 1));
        }

        public PackageCertificateToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            // These values are regularly changing. Don't record them so test data is stable.
            ConfigureWorkerSettings = x => x.RecordCertificateStatus = false;
        }

        protected override string DestinationContainerName1 => Options.Value.PackageCertificateContainerName;
        protected override string DestinationContainerName2 => Options.Value.CertificateContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageCertificateToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[]
            {
                "CatalogScan-" + CatalogScanDriverType.LoadPackageArchive,
            });
        }

        private List<string> AdditionalTableNames { get; } = new();

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[]
            {
                Options.Value.PackageArchiveTableName,
                Options.Value.PackageToCertificateTableName,
                Options.Value.CertificateToPackageTableName,
            }).Concat(AdditionalTableNames);
        }

        private List<string> AdditionalLeaseNames { get; } = new();

        protected override IEnumerable<string> GetExpectedLeaseNames()
        {
            return base.GetExpectedLeaseNames().Concat(AdditionalLeaseNames);
        }

        private void SetupCleanupOrphans()
        {
            AdditionalLeaseNames.Add($"CleanupOrphans-{ReferenceTypes.Package}-{ReferenceTypes.Certificate}");
            AdditionalTableNames.Add(Options.Value.TaskStateTableName);
        }

        protected async Task CleanupOrphansAsync()
        {
            var cleanup = Host.Services.GetRequiredService<ICleanupOrphanRecordsService<CertificateRecord>>();
            await cleanup.InitializeAsync();
            Assert.True(await cleanup.StartAsync());
            await ProcessQueueAsync(async () => !await cleanup.IsRunningAsync());
        }

        private async Task<Target[]> GetTableOutputAsync(int step)
        {
            return await Task.WhenAll(
                GetOwnerToSubjectAsync(step),
                GetSubjectToOwnerAsync(step));
        }

        private async Task<Target> GetOwnerToSubjectAsync(int step)
        {
            return await GetOwnerToSubjectAsync(
                Options.Value.PackageToCertificateTableName,
                bytes =>
                {
                    var data = MessagePackSerializer.Deserialize<CertificateRelationshipTypes>(
                        bytes,
                        NuGetInsightsMessagePack.Options);
                    return Enum
                        .GetValues<CertificateRelationshipTypes>()
                        .Except(new[] { CertificateRelationshipTypes.None })
                        .Where(x => data.HasFlag(x))
                        .Select(x => Enum.GetName(x))
                        .ToList();
                },
                step);
        }

        private async Task AssertEmptyOwnerToSubjectAsync()
        {
            await AssertEmptyTableAsync(Options.Value.PackageToCertificateTableName);
        }

        private async Task<Target> GetSubjectToOwnerAsync(int step)
        {
            return await GetSubjectToOwnerAsync(Options.Value.CertificateToPackageTableName, step);
        }

        private async Task AssertEmptySubjectToOwnerAsync()
        {
            await AssertEmptyTableAsync(Options.Value.CertificateToPackageTableName);
        }
    }
}
