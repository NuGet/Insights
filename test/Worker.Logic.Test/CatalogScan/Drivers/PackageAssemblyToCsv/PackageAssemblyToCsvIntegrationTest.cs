// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;

namespace NuGet.Insights.Worker.PackageAssemblyToCsv
{
    public class PackageAssemblyToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageAssembly>
    {
        private const string PackageAssemblyToCsvDir = nameof(PackageAssemblyToCsv);
        private const string PackageAssemblyToCsv_WithDeleteDir = nameof(PackageAssemblyToCsv_WithDelete);
        private const string PackageAssemblyToCsv_WithUnmanagedDir = nameof(PackageAssemblyToCsv_WithUnmanaged);
        private const string PackageAssemblyToCsv_WithDuplicatesDir = nameof(PackageAssemblyToCsv_WithDuplicates);

        [Fact]
        public async Task PackageAssemblyToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssemblyToCsvDir, Step1, 0);
            await AssertOutputAsync(PackageAssemblyToCsvDir, Step1, 1);
            await AssertOutputAsync(PackageAssemblyToCsvDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageAssemblyToCsvDir, Step2, 0);
            await AssertOutputAsync(PackageAssemblyToCsvDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageAssemblyToCsvDir, Step2, 2);
        }

        [Fact]
        public async Task PackageAssemblyToCsv_WithDiskBuffering()
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
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssemblyToCsvDir, Step1, 0);
            await AssertOutputAsync(PackageAssemblyToCsvDir, Step1, 1);
            await AssertOutputAsync(PackageAssemblyToCsvDir, Step1, 2);
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
        public async Task PackageAssemblyToCsv_WithDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssemblyToCsv_WithDeleteDir, Step1, 0);
            await AssertOutputAsync(PackageAssemblyToCsv_WithDeleteDir, Step1, 1);
            await AssertOutputAsync(PackageAssemblyToCsv_WithDeleteDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageAssemblyToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
            await AssertOutputAsync(PackageAssemblyToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageAssemblyToCsv_WithDeleteDir, Step2, 2);
        }

        [Fact]
        public async Task PackageAssemblyToCsv_WithUnmanaged()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            var min0 = DateTimeOffset.Parse("2018-08-29T04:22:56.6184931Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2018-08-29T04:24:40.3247223Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssemblyToCsv_WithUnmanagedDir, Step1, 0);
        }

        [Fact]
        public Task PackageAssemblyToCsv_WithDuplicates_OnlyLatestLeaves()
        {
            return PackageAssemblyToCsv_WithDuplicates();
        }

        [Fact]
        public Task PackageAssemblyToCsv_WithDuplicates_AllLeaves()
        {
            MutableLatestLeavesTypes.Clear();
            return PackageAssemblyToCsv_WithDuplicates();
        }

        public PackageAssemblyToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
            MutableLatestLeavesTypes.Add(DriverType);
        }

        private List<string> AdditionalLeaseNames { get; } = new();

        protected override IEnumerable<string> GetExpectedLeaseNames()
        {
            return base.GetExpectedLeaseNames().Concat(AdditionalLeaseNames);
        }

        protected override string DestinationContainerName => Options.Value.PackageAssemblyContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageAssemblyToCsv;
        private List<CatalogScanDriverType> MutableLatestLeavesTypes { get; } = new();
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => MutableLatestLeavesTypes;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        private async Task PackageAssemblyToCsv_WithDuplicates()
        {
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssemblyToCsv_WithDuplicatesDir, Step1, 0);

            var duplicatePackageRequests = HttpMessageHandlerFactory
                .SuccessRequests
                .Where(x => x.RequestUri.GetLeftPart(UriPartial.Path).EndsWith("/gosms.ge-sms-api.1.0.1.nupkg", StringComparison.Ordinal))
                .ToList();
            Assert.Equal(LatestLeavesTypes.Contains(DriverType) ? 1 : 2, duplicatePackageRequests.Count());
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageHashesTableName });
        }
    }
}
