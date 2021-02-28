using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.PackageArchiveEntryToCsv
{
    public class PackageArchiveEntryToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageArchiveEntry>
    {
        private const string PackageArchiveEntryToCsvDir = nameof(PackageArchiveEntryToCsv);
        private const string PackageArchiveEntryToCsv_WithDeleteDir = nameof(PackageArchiveEntryToCsv_WithDelete);
        private const string PackageArchiveEntryToCsv_WithDuplicateEntriesDir = nameof(PackageArchiveEntryToCsv_WithDuplicateEntries);

        public PackageArchiveEntryToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageArchiveEntryContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageArchiveEntryToCsv;

        public class PackageArchiveEntryToCsv : PackageArchiveEntryToCsvIntegrationTest
        {
            public PackageArchiveEntryToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageArchiveEntryToCsvDir, Step1, 0);
                await AssertOutputAsync(PackageArchiveEntryToCsvDir, Step1, 1);
                await AssertOutputAsync(PackageArchiveEntryToCsvDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageArchiveEntryToCsvDir, Step2, 0);
                await AssertOutputAsync(PackageArchiveEntryToCsvDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(PackageArchiveEntryToCsvDir, Step2, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class PackageArchiveEntryToCsv_WithDelete : PackageArchiveEntryToCsvIntegrationTest
        {
            public PackageArchiveEntryToCsv_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/behaviorsample.1.0.0.nupkg"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/behaviorsample.1.0.0.nupkg");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };
                var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
                var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
                var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageArchiveEntryToCsv_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(PackageArchiveEntryToCsv_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(PackageArchiveEntryToCsv_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageArchiveEntryToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(PackageArchiveEntryToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(PackageArchiveEntryToCsv_WithDeleteDir, Step2, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class PackageArchiveEntryToCsv_WithDuplicateEntries : PackageArchiveEntryToCsvIntegrationTest
        {
            public PackageArchiveEntryToCsv_WithDuplicateEntries(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2019-12-03T16:44:42.3383514Z");
                var max1 = DateTimeOffset.Parse("2019-12-03T16:44:55.0668686Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageArchiveEntryToCsv_WithDuplicateEntriesDir, Step1, 0);
                await AssertOutputAsync(PackageArchiveEntryToCsv_WithDuplicateEntriesDir, Step1, 1);
                await AssertOutputAsync(PackageArchiveEntryToCsv_WithDuplicateEntriesDir, Step1, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
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
