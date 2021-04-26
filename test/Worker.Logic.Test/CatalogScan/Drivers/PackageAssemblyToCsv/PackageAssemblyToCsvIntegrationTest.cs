using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.PackageAssemblyToCsv
{
    public class PackageAssemblyToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageAssembly>
    {
        private const string PackageAssemblyToCsvDir = nameof(PackageAssemblyToCsv);
        private const string PackageAssemblyToCsv_WithDeleteDir = nameof(PackageAssemblyToCsv_WithDelete);
        private const string PackageAssemblyToCsv_WithUnmanagedDir = nameof(PackageAssemblyToCsv_WithUnmanaged);
        private const string PackageAssemblyToCsv_WithDuplicatesDir = nameof(PackageAssemblyToCsv_WithDuplicates);

        public class PackageAssemblyToCsv : PackageAssemblyToCsvIntegrationTest
        {
            public PackageAssemblyToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
        }

        public class PackageAssemblyToCsv_WithDiskBuffering : PackageAssemblyToCsvIntegrationTest
        {
            public PackageAssemblyToCsv_WithDiskBuffering(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");

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

            protected override IEnumerable<string> GetExpectedLeaseNames()
            {
                return base.GetExpectedLeaseNames().Concat(new[] { TempDirLeaseName });
            }
        }

        public class PackageAssemblyToCsv_WithDelete : PackageAssemblyToCsvIntegrationTest
        {
            public PackageAssemblyToCsv_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
        }

        public class PackageAssemblyToCsv_WithUnmanaged : PackageAssemblyToCsvIntegrationTest
        {
            public PackageAssemblyToCsv_WithUnmanaged(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                // Arrange
                var min0 = DateTimeOffset.Parse("2018-08-29T04:22:56.6184931Z");
                var max1 = DateTimeOffset.Parse("2018-08-29T04:24:40.3247223Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageAssemblyToCsv_WithUnmanagedDir, Step1, 0);
            }
        }

        public class PackageAssemblyToCsv_WithDuplicates_OnlyLatestLeaves : PackageAssemblyToCsvIntegrationTest
        {
            public PackageAssemblyToCsv_WithDuplicates_OnlyLatestLeaves(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public Task Execute()
            {
                return PackageAssemblyToCsv_WithDuplicates();
            }
        }

        public class PackageAssemblyToCsv_WithDuplicates_AllLeaves : PackageAssemblyToCsvIntegrationTest
        {
            public PackageAssemblyToCsv_WithDuplicates_AllLeaves(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => Enumerable.Empty<CatalogScanDriverType>();

            [Fact]
            public Task Execute()
            {
                return PackageAssemblyToCsv_WithDuplicates();
            }
        }

        public PackageAssemblyToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageAssemblyContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageAssemblyToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        private async Task PackageAssemblyToCsv_WithDuplicates()
        {
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageAssemblyToCsv_WithDuplicatesDir, Step1, 0);

            var duplicatePackageRequests = HttpMessageHandlerFactory
                .Requests
                .Where(x => x.RequestUri.AbsolutePath.EndsWith("/gosms.ge-sms-api.1.0.1.nupkg"))
                .ToList();
            Assert.Equal(LatestLeavesTypes.Contains(DriverType) ? 1 : 2, duplicatePackageRequests.Count);
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageHashesTableName });
        }
    }
}
