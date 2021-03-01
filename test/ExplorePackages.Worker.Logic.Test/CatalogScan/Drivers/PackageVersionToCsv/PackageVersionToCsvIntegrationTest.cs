using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.PackageVersionToCsv
{
    public class PackageVersionToCsvIntegrationTest : BaseCatalogScanToCsvIntegrationTest<PackageVersionRecord>
    {
        private const string PackageVersionToCsvDir = nameof(PackageVersionToCsv);
        private const string PackageVersionToCsv_WithDeleteDir = nameof(PackageVersionToCsv_WithDelete);
        private const string PackageVersionToCsv_WithDuplicatesDir = nameof(PackageVersionToCsv_WithDuplicates);
        private const string PackageVersionToCsv_WithAllLatestDir = nameof(PackageVersionToCsv_WithAllLatest);

        public PackageVersionToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        public override bool OnlyLatestLeavesPerId => true;
        public override bool OnlyLatestLeaves => true;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageVersionToCsv;
        protected override string DestinationContainerName => Options.Value.PackageVersionContainerName;

        public class PackageVersionToCsv : PackageVersionToCsvIntegrationTest
        {
            public PackageVersionToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");
                var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageVersion, min0);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max1);
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageVersionToCsvDir, Step1, 0);
                await AssertOutputAsync(PackageVersionToCsvDir, Step1, 1);
                await AssertOutputAsync(PackageVersionToCsvDir, Step1, 2);

                // Act
                await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max2);
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageVersionToCsvDir, Step2, 0);
                await AssertOutputAsync(PackageVersionToCsvDir, Step2, 1);
                await AssertOutputAsync(PackageVersionToCsvDir, Step2, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class PackageVersionToCsv_WithDelete : PackageVersionToCsvIntegrationTest
        {
            public PackageVersionToCsv_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
                var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
                var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageVersion, min0);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max1);
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max2);
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step2, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class PackageVersionToCsv_WithDuplicates : PackageVersionToCsvIntegrationTest
        {
            public PackageVersionToCsv_WithDuplicates(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageVersion, min0);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max1);
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageVersionToCsv_WithDuplicatesDir, Step1, 0);
                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class PackageVersionToCsv_WithAllLatest : PackageVersionToCsvIntegrationTest
        {
            public PackageVersionToCsv_WithAllLatest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2021-02-28T01:06:32.8546849Z").AddTicks(-1);
                var max1 = DateTimeOffset.Parse("2021-02-28T01:06:32.8546849Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageVersion, min0);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max1);
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageVersionToCsv_WithAllLatestDir, Step1, 0);
                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        protected override IEnumerable<string> GetExpectedLeaseNames()
        {
            return base.GetExpectedLeaseNames().Concat(new[] { "Start-CatalogScan-" + CatalogScanDriverType.LoadPackageVersion });
        }

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[] { "CatalogScan-" + CatalogScanDriverType.LoadPackageVersion });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.PackageVersionTableName;
        }
    }
}
