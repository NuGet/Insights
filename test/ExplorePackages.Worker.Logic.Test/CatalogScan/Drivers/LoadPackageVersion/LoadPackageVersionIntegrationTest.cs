using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.LoadPackageVersion
{
    public class LoadPackageVersionIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string LoadPackageVersionDir = nameof(LoadPackageVersion);
        private const string LoadPackageVersion_WithDeleteDir = nameof(LoadPackageVersion_WithDelete);
        private const string LoadPackageVersion_WithDuplicatesDir = nameof(LoadPackageVersion_WithDuplicates);

        public class LoadPackageVersion : LoadPackageVersionIntegrationTest
        {
            public LoadPackageVersion(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z");
                var max1 = DateTimeOffset.Parse("2020-12-27T05:07:21.9968244Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(LoadPackageVersionDir, Step1);
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class LoadPackageVersion_WithDelete : LoadPackageVersionIntegrationTest
        {
            public LoadPackageVersion_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(LoadPackageVersion_WithDeleteDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(LoadPackageVersion_WithDeleteDir, Step2);
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class LoadPackageVersion_WithDuplicates : LoadPackageVersionIntegrationTest
        {
            public LoadPackageVersion_WithDuplicates(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(LoadPackageVersion_WithDuplicatesDir, Step1);
                AssertOnlyInfoLogsOrLess();
            }
        }

        public LoadPackageVersionIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadPackageVersion;
        public override bool OnlyLatestLeaves => true;
        public override bool OnlyLatestLeavesPerId => false;

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.PackageVersionTableName;
        }

        private async Task AssertOutputAsync(string dir, string stepName)
        {
            var table = ServiceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(Options.Value.PackageVersionTableName);

            await AssertEntityOutputAsync<PackageVersionEntity>(table, Path.Combine(dir, stepName));
        }
    }
}
