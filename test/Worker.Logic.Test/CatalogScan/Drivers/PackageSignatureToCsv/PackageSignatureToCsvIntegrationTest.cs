using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageSignatureToCsv
{
    public class PackageSignatureToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageSignature>
    {
        private const string PackageSignatureToCsvDir = nameof(PackageSignatureToCsv);
        private const string PackageSignatureToCsv_WithDuplicatesInCommitDir = nameof(PackageSignatureToCsv_WithDuplicatesInCommit);
        private const string PackageSignatureToCsv_AuthorSignatureDir = nameof(PackageSignatureToCsv_AuthorSignature);
        private const string PackageSignatureToCsv_BadTimestampDir = nameof(PackageSignatureToCsv_BadTimestamp);
        private const string PackageSignatureToCsv_WithDeleteDir = nameof(PackageSignatureToCsv_WithDelete);

        public class PackageSignatureToCsv : PackageSignatureToCsvIntegrationTest
        {
            public PackageSignatureToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageSignatureToCsvDir, Step1, 0);
                await AssertOutputAsync(PackageSignatureToCsvDir, Step1, 1);
                await AssertOutputAsync(PackageSignatureToCsvDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageSignatureToCsvDir, Step2, 0);
                await AssertOutputAsync(PackageSignatureToCsvDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(PackageSignatureToCsvDir, Step2, 2);
            }
        }

        public class PackageSignatureToCsv_WithDuplicatesInCommit : PackageSignatureToCsvIntegrationTest
        {
            public PackageSignatureToCsv_WithDuplicatesInCommit(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2018-03-23T08:55:02.1875809Z");
                var max1 = DateTimeOffset.Parse("2018-03-23T08:55:20.0232708Z");
                var max2 = DateTimeOffset.Parse("2018-03-23T08:55:38.0342003Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step1, 0);
                await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step1, 1);
                await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step2, 1);
                await AssertOutputAsync(PackageSignatureToCsv_WithDuplicatesInCommitDir, Step2, 2);
            }
        }

        public class PackageSignatureToCsv_AuthorSignature : PackageSignatureToCsvIntegrationTest
        {
            public PackageSignatureToCsv_AuthorSignature(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-03-04T22:55:23.8646211Z");
                var max1 = DateTimeOffset.Parse("2020-03-04T22:56:51.1816512Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageSignatureToCsv_AuthorSignatureDir, Step1, 0);
            }
        }

        public class PackageSignatureToCsv_BadTimestamp : PackageSignatureToCsvIntegrationTest
        {
            public PackageSignatureToCsv_BadTimestamp(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-04T15:12:14Z");
                var max1 = DateTimeOffset.Parse("2020-11-04T15:12:15.7221964Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageSignatureToCsv_BadTimestampDir, Step1, 0);
            }
        }

        public class PackageSignatureToCsv_WithDelete : PackageSignatureToCsvIntegrationTest
        {
            public PackageSignatureToCsv_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(PackageSignatureToCsv_WithDeleteDir, Step2, 2);
            }
        }

        public PackageSignatureToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageSignatureContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageSignatureToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

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
