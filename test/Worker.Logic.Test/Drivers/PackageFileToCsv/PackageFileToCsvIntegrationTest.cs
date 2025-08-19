// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;

namespace NuGet.Insights.Worker.PackageFileToCsv
{
    public class PackageFileToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageFileRecord>
    {
        private const string PackageFileToCsvDir = nameof(PackageFileToCsv);
        private const string PackageFileToCsv_WithDeleteDir = nameof(PackageFileToCsv_WithDelete);
        private const string PackageFileToCsv_WithDuplicatesDir = nameof(PackageFileToCsv_WithDuplicates);

        [Fact]
        public async Task PackageFileToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, min0);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageArchive, onlyLatestLeaves: true, max1);
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageFileToCsvDir, Step1, 0);
            await AssertPackageHashesTableAsync(PackageFileToCsvDir, Step1);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageArchive, onlyLatestLeaves: true, max2);
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageFileToCsvDir, Step2, 0);
            await AssertPackageHashesTableAsync(PackageFileToCsvDir, Step2);
        }

        [Fact]
        public async Task PackageFileToCsv_WithDiskBuffering()
        {
            // Arrange
            ConfigureSettings = x =>
            {
                x.MaxTempMemoryStreamSize = 0;
                x.TempDirectories[0].MaxConcurrentWriters = 1;
            };

            AdditionalLeaseNames.Add(TempDirLeaseName);

            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, min0);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageArchive, onlyLatestLeaves: true, max1);
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageFileToCsvDir, Step1, 0);
            await AssertPackageHashesTableAsync(PackageFileToCsvDir, Step1);
        }

        public string TempDirLeaseName
        {
            get
            {
                using (var sha256 = SHA256.Create())
                {
                    var path = Path.GetFullPath(Options.Value.TempDirectories[0].Path);
                    var bytes = Encoding.UTF8.GetBytes(path.ToLowerInvariant());
                    return $"TempStreamDirectory-{sha256.ComputeHash(bytes).ToTrimmedBase32()}-Semaphore-0";
                }
            }
        }

        [Fact]
        public async Task PackageFileToCsv_WithDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, min0);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageArchive, onlyLatestLeaves: true, max1);
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageFileToCsv_WithDeleteDir, Step1, 0);
            await AssertPackageHashesTableAsync(PackageFileToCsv_WithDeleteDir, Step1);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageArchive, onlyLatestLeaves: true, max2);
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageFileToCsv_WithDeleteDir, Step2, 0);
            await AssertPackageHashesTableAsync(PackageFileToCsv_WithDeleteDir, Step2);
        }

        [Fact]
        public Task PackageFileToCsv_WithDuplicates_OnlyLatestLeaves()
        {
            return PackageFileToCsv_WithDuplicates();
        }

        [Fact]
        public Task PackageFileToCsv_WithDuplicates_AllLeaves()
        {
            MutableLatestLeavesTypes.Remove(DriverType);
            return PackageFileToCsv_WithDuplicates();
        }

        public PackageFileToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
            MutableLatestLeavesTypes.Add(DriverType);
        }

        private List<string> AdditionalLeaseNames { get; } = ["Start-" + CatalogScanDriverType.LoadPackageArchive];

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(
            [
                "CatalogScan-" + CatalogScanDriverType.LoadPackageArchive,
            ]);
        }

        protected override IEnumerable<string> GetExpectedLeaseNames()
        {
            return base.GetExpectedLeaseNames().Concat(AdditionalLeaseNames);
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(
            [
                Options.Value.PackageArchiveTableName,
                Options.Value.PackageHashesTableName
            ]);
        }

        protected override string DestinationContainerName => Options.Value.PackageFileContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageFileToCsv;
        private List<CatalogScanDriverType> MutableLatestLeavesTypes { get; } = [CatalogScanDriverType.LoadPackageArchive];
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => MutableLatestLeavesTypes;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => [];

        private async Task PackageFileToCsv_WithDuplicates()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, min0);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageArchive, onlyLatestLeaves: true, max1);
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageFileToCsv_WithDuplicatesDir, Step1, 0);
            await AssertPackageHashesTableAsync(PackageFileToCsv_WithDuplicatesDir, Step1);

            var duplicatePackageRequests = HttpMessageHandlerFactory
                .Responses
                .Where(x => x.IsSuccessStatusCode && x.Content.Headers.ContentLength.HasValue)
                .Select(x => x.RequestMessage)
                .Where(x => x.RequestUri.GetLeftPart(UriPartial.Path).EndsWith("/gosms.ge-sms-api.1.0.1.nupkg", StringComparison.Ordinal))
                .ToList();
            Assert.Single(duplicatePackageRequests, r => r.Method == HttpMethod.Head);
            Assert.Equal(LatestLeavesTypes.Contains(DriverType) ? 2 : 3, duplicatePackageRequests.Where(r => r.Method == HttpMethod.Get).Count());
        }
    }
}
