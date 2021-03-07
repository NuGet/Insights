using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.NuGetPackageExplorerToCsv
{
    public class NuGetPackageExplorerToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<NuGetPackageExplorerRecord>
    {
        private const string NuGetPackageExplorerToCsvDir = nameof(NuGetPackageExplorerToCsv);
        private const string NuGetPackageExplorerToCsv_WithDeleteDir = nameof(NuGetPackageExplorerToCsv_WithDelete);

        public class NuGetPackageExplorerToCsv : NuGetPackageExplorerToCsvIntegrationTest
        {
            public NuGetPackageExplorerToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-10T23:37:14.085963Z");
                var max1 = DateTimeOffset.Parse("2020-11-10T23:38:46.5558967Z");
                var max2 = DateTimeOffset.Parse("2020-11-10T23:39:05.5717605Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadLatestPackageLeaf, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(NuGetPackageExplorerToCsvDir, Step1, 0);
                await AssertOutputAsync(NuGetPackageExplorerToCsvDir, Step1, 1);
                await AssertOutputAsync(NuGetPackageExplorerToCsvDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(NuGetPackageExplorerToCsvDir, Step2, 0);
                await AssertOutputAsync(NuGetPackageExplorerToCsvDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(NuGetPackageExplorerToCsvDir, Step2, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class NuGetPackageExplorerToCsv_WithDelete : NuGetPackageExplorerToCsvIntegrationTest
        {
            public NuGetPackageExplorerToCsv_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
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
                await SetCursorAsync(CatalogScanDriverType.LoadLatestPackageLeaf, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(NuGetPackageExplorerToCsv_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(NuGetPackageExplorerToCsv_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(NuGetPackageExplorerToCsv_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(NuGetPackageExplorerToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(NuGetPackageExplorerToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(NuGetPackageExplorerToCsv_WithDeleteDir, Step2, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public NuGetPackageExplorerToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.NuGetPackageExplorerContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.NuGetPackageExplorerToCsv;
        public override bool OnlyLatestLeaves => true;
        public override bool OnlyLatestLeavesPerId => false;

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[] { "CatalogScan-" + CatalogScanDriverType.LoadLatestPackageLeaf });
        }
    }
}
