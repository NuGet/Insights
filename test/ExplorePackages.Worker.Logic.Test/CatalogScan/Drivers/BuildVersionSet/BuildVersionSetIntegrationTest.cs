using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public class BuildVersionSetIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string BuildVersionSetDir = nameof(BuildVersionSet);
        private const string BuildVersionSet_WithDeleteDir = nameof(BuildVersionSet_WithDelete);
        private const string BuildVersionSet_WithDuplicatesDir = nameof(BuildVersionSet_WithDuplicates);

        public class BuildVersionSet : BuildVersionSetIntegrationTest
        {
            public BuildVersionSet(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                // await AssertOutputAsync(BuildVersionSetDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                // await AssertOutputAsync(BuildVersionSetDir, Step2);
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class BuildVersionSet_WithDelete : BuildVersionSetIntegrationTest
        {
            public BuildVersionSet_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
                var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
                var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                // await AssertOutputAsync(BuildVersionSet_WithDeleteDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                // await AssertOutputAsync(BuildVersionSet_WithDeleteDir, Step2);
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class BuildVersionSet_WithDuplicates : BuildVersionSetIntegrationTest
        {
            public BuildVersionSet_WithDuplicates(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                // await AssertOutputAsync(BuildVersionSet_WithDuplicatesDir, Step1);
                AssertOnlyInfoLogsOrLess();
            }
        }

        public BuildVersionSetIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.BuildVersionSet;
        public override bool OnlyLatestLeaves => false;
        public override bool OnlyLatestLeavesPerId => false;
    }
}
