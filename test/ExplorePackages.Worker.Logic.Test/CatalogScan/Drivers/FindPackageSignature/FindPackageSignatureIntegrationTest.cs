using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.FindPackageSignature
{
    public class FindPackageSignatureIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest
    {
        private const string FindPackageSignatureDir = nameof(FindPackageSignature);
        private const string FindPackageSignature_AuthorSignatureDir = nameof(FindPackageSignature_AuthorSignature);
        private const string FindPackageSignature_WithDeleteDir = nameof(FindPackageSignature_WithDelete);

        public FindPackageSignatureIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageSignatureContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.FindPackageSignature;

        public class FindPackageSignature : FindPackageSignatureIntegrationTest
        {
            public FindPackageSignature(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindPackageSignatureDir, Step1, 0);
                await AssertOutputAsync(FindPackageSignatureDir, Step1, 1);
                await AssertOutputAsync(FindPackageSignatureDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(FindPackageSignatureDir, Step2, 0);
                await AssertOutputAsync(FindPackageSignatureDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(FindPackageSignatureDir, Step2, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }
        public class FindPackageSignature_AuthorSignature : FindPackageSignatureIntegrationTest
        {
            public FindPackageSignature_AuthorSignature(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-03-04T22:55:23.8646211Z");
                var max1 = DateTimeOffset.Parse("2020-03-04T22:56:51.1816512Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindPackageSignature_AuthorSignatureDir, Step1, 0);
                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class FindPackageSignature_WithDelete : FindPackageSignatureIntegrationTest
        {
            public FindPackageSignature_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindPackageSignature_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(FindPackageSignature_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(FindPackageSignature_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(FindPackageSignature_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(FindPackageSignature_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(FindPackageSignature_WithDeleteDir, Step2, 2);

                await AssertExpectedStorageAsync();
                AssertOnlyInfoLogsOrLess();
            }
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageFileTableName });
        }
    }
}
