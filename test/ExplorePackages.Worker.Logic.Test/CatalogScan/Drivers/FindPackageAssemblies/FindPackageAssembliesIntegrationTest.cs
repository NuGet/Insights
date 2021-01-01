using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.FindPackageAssemblies
{
    public class FindPackageAssembliesIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest
    {
        private const string FindPackageAssembliesDir = nameof(FindPackageAssemblies);
        private const string FindPackageAssemblies_WithDeleteDir = nameof(FindPackageAssemblies_WithDelete);
        private const string FindPackageAssemblies_WithUnmanagedDir = nameof(FindPackageAssemblies_WithUnmanaged);
        private const string FindPackageAssemblies_WithDuplicatesDir = nameof(FindPackageAssemblies_WithDuplicates);

        public FindPackageAssembliesIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.FindPackageAssembliesContainerName;

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.FindPackageAssemblies;

        public class FindPackageAssemblies : FindPackageAssembliesIntegrationTest
        {
            public FindPackageAssemblies(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await AssertOutputAsync(FindPackageAssembliesDir, Step1, 0);
                await AssertOutputAsync(FindPackageAssembliesDir, Step1, 1);
                await AssertOutputAsync(FindPackageAssembliesDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(FindPackageAssembliesDir, Step2, 0);
                await AssertOutputAsync(FindPackageAssembliesDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(FindPackageAssembliesDir, Step2, 2);

                await VerifyExpectedStorageAsync();
            }
        }

        public class FindPackageAssemblies_WithDiskBuffering : FindPackageAssembliesIntegrationTest
        {
            public FindPackageAssemblies_WithDiskBuffering(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureSettings = x =>
                {
                    x.MaxTempMemoryStreamSize = 0;
                    x.TempDirectories[0].MaxConcurrentWriters = 1;
                };

                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindPackageAssembliesDir, Step1, 0);
                await AssertOutputAsync(FindPackageAssembliesDir, Step1, 1);
                await AssertOutputAsync(FindPackageAssembliesDir, Step1, 2);

                await VerifyExpectedStorageAsync();
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

            protected override IEnumerable<string> GetExpectedLeaseNames()
            {
                return base.GetExpectedLeaseNames().Concat(new[] { TempDirLeaseName });
            }
        }

        public class FindPackageAssemblies_WithDelete : FindPackageAssembliesIntegrationTest
        {
            public FindPackageAssemblies_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await AssertOutputAsync(FindPackageAssemblies_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(FindPackageAssemblies_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(FindPackageAssemblies_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(FindPackageAssemblies_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(FindPackageAssemblies_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(FindPackageAssemblies_WithDeleteDir, Step2, 2);

                await VerifyExpectedStorageAsync();
            }
        }

        public class FindPackageAssemblies_WithUnmanaged : FindPackageAssembliesIntegrationTest
        {
            public FindPackageAssemblies_WithUnmanaged(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2018-08-29T04:22:56.6184931Z");
                var max1 = DateTimeOffset.Parse("2018-08-29T04:24:40.3247223Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(FindPackageAssemblies_WithUnmanagedDir, Step1, 0);

                await VerifyExpectedStorageAsync();
            }
        }

        public class FindPackageAssemblies_WithDuplicates_OnlyLatestLeaves : FindPackageAssembliesIntegrationTest
        {
            public FindPackageAssemblies_WithDuplicates_OnlyLatestLeaves(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            public override bool OnlyLatestLeaves => true;

            [Fact]
            public Task Execute() => FindPackageAssemblies_WithDuplicates();
        }

        public class FindPackageAssemblies_WithDuplicates_AllLeaves : FindPackageAssembliesIntegrationTest
        {
            public FindPackageAssemblies_WithDuplicates_AllLeaves(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            public override bool OnlyLatestLeaves => false;

            [Fact]
            public Task Execute() => FindPackageAssemblies_WithDuplicates();
        }

        private async Task FindPackageAssemblies_WithDuplicates()
        {
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(FindPackageAssemblies_WithDuplicatesDir, Step1, 0);

            var duplicatePackageRequests = HttpMessageHandlerFactory
                .Requests
                .Where(x => x.RequestUri.AbsolutePath.EndsWith("/gosms.ge-sms-api.1.0.1.nupkg"))
                .ToList();
            Assert.Equal(OnlyLatestLeaves ? 1 : 2, duplicatePackageRequests.Count);

            await VerifyExpectedStorageAsync();
        }
    }
}
