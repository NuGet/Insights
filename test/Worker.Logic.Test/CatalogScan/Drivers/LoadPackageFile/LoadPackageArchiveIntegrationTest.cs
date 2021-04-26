using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MessagePack;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.LoadPackageArchive
{
    public class LoadPackageArchiveIntegrationTest : BaseCatalogScanIntegrationTest
    {
        public const string LoadPackageArchiveDir = nameof(LoadPackageArchive);
        public const string LoadPackageArchive_WithDeleteDir = nameof(LoadPackageArchive_WithDelete);

        public class LoadPackageArchive : LoadPackageArchiveIntegrationTest
        {
            public LoadPackageArchive(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await AssertOutputAsync(LoadPackageArchiveDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(LoadPackageArchiveDir, Step2);
            }
        }

        public class LoadPackageArchive_WithDelete : LoadPackageArchiveIntegrationTest
        {
            public LoadPackageArchive_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await AssertOutputAsync(LoadPackageArchive_WithDeleteDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(LoadPackageArchive_WithDeleteDir, Step2);
            }
        }

        public LoadPackageArchiveIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadPackageArchive;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageArchiveTableName });
        }

        private async Task AssertOutputAsync(string testName, string stepName)
        {
            await AssertWideEntityOutputAsync(
                Options.Value.PackageArchiveTableName,
                Path.Combine(testName, stepName),
                stream =>
                {
                    var entity = MessagePackSerializer.Deserialize<PackageFileService.PackageFileInfoVersions>(stream, ExplorePackagesMessagePack.Options);

                    string mzipHash = null;
                    string signatureHash = null;
                    SortedDictionary<string, List<string>> httpHeaders = null;

                    if (entity.V1.Available)
                    {
                        using var algorithm = SHA256.Create();
                        mzipHash = algorithm.ComputeHash(entity.V1.MZipBytes.ToArray()).ToHex();
                        signatureHash = algorithm.ComputeHash(entity.V1.SignatureBytes.ToArray()).ToHex();
                        httpHeaders = NormalizeHeaders(entity.V1.HttpHeaders);
                    }

                    return new
                    {
                        entity.V1.Available,
                        entity.V1.CommitTimestamp,
                        HttpHeaders = httpHeaders,
                        MZipHash = mzipHash,
                        SignatureHash = signatureHash,
                    };
                });
        }
    }
}
