using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.PackageArchiveToCsv
{
    public class PackageArchiveToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageArchiveRecord, PackageArchiveEntry>
    {
        private const string PackageArchiveToCsvDir = nameof(PackageArchiveToCsv);
        private const string PackageArchiveToCsv_WithDeleteDir = nameof(PackageArchiveToCsv_WithDelete);
        private const string PackageArchiveToCsv_WithDuplicateEntriesDir = nameof(PackageArchiveToCsv_WithDuplicateEntries);

        public class PackageArchiveToCsv : PackageArchiveToCsvIntegrationTest
        {
            public PackageArchiveToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");
                var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
                await SetCursorAsync(CatalogScanDriverType.PackageAssemblyToCsv, min0);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(CatalogScanDriverType.PackageAssemblyToCsv, onlyLatestLeaves: true, max1);
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageArchiveToCsvDir, Step1, 0);
                await AssertOutputAsync(PackageArchiveToCsvDir, Step1, 1);
                await AssertOutputAsync(PackageArchiveToCsvDir, Step1, 2);

                // Act
                await UpdateAsync(CatalogScanDriverType.PackageAssemblyToCsv, onlyLatestLeaves: true, max2);
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageArchiveToCsvDir, Step2, 0);
                await AssertOutputAsync(PackageArchiveToCsvDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(PackageArchiveToCsvDir, Step2, 2);
            }
        }

        public class PackageArchiveToCsv_WithDelete : PackageArchiveToCsvIntegrationTest
        {
            public PackageArchiveToCsv_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                MakeDeletedPackageAvailable();
                var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
                var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
                var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
                await SetCursorAsync(CatalogScanDriverType.PackageAssemblyToCsv, min0);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(CatalogScanDriverType.PackageAssemblyToCsv, onlyLatestLeaves: true, max1);
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageArchiveToCsv_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(PackageArchiveToCsv_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(PackageArchiveToCsv_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(CatalogScanDriverType.PackageAssemblyToCsv, onlyLatestLeaves: true, max2);
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageArchiveToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(PackageArchiveToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(PackageArchiveToCsv_WithDeleteDir, Step2, 2);
            }
        }

        public class PackageArchiveToCsv_WithDuplicateEntries : PackageArchiveToCsvIntegrationTest
        {
            public PackageArchiveToCsv_WithDuplicateEntries(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2019-12-03T16:44:42.3383514Z");
                var max1 = DateTimeOffset.Parse("2019-12-03T16:44:55.0668686Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
                await SetCursorAsync(CatalogScanDriverType.PackageAssemblyToCsv, min0);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(CatalogScanDriverType.PackageAssemblyToCsv, onlyLatestLeaves: true, max1);
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageArchiveToCsv_WithDuplicateEntriesDir, Step1, 0);
                await AssertOutputAsync(PackageArchiveToCsv_WithDuplicateEntriesDir, Step1, 1);
                await AssertOutputAsync(PackageArchiveToCsv_WithDuplicateEntriesDir, Step1, 2);
            }
        }

        public PackageArchiveToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName1 => Options.Value.PackageArchiveContainerName;
        protected override string DestinationContainerName2 => Options.Value.PackageArchiveEntryContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageArchiveToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType, CatalogScanDriverType.PackageAssemblyToCsv };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[]
            {
                "CatalogScan-" + CatalogScanDriverType.LoadPackageArchive,
                "CatalogScan-" + CatalogScanDriverType.PackageAssemblyToCsv,
            });
        }

        protected override IEnumerable<string> GetExpectedLeaseNames()
        {
            return base.GetExpectedLeaseNames().Concat(new[]
            {
                "Start-CatalogScan-" + CatalogScanDriverType.PackageAssemblyToCsv,
            });
        }

        protected override IEnumerable<string> GetExpectedBlobContainerNames()
        {
            return base.GetExpectedBlobContainerNames().Concat(new[]
            {
                Options.Value.PackageAssemblyContainerName,
            });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[]
            {
                Options.Value.PackageArchiveTableName,
                Options.Value.PackageHashesTableName,
            });
        }
    }
}
