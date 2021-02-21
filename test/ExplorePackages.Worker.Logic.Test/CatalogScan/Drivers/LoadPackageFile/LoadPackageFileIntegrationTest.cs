using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.LoadPackageFile
{
    public class LoadPackageFileIntegrationTest : BaseCatalogScanIntegrationTest
    {
        public const string LoadPackageFileDir = nameof(LoadPackageFile);
        public const string LoadPackageFile_WithDeleteDir = nameof(LoadPackageFile_WithDelete);

        public class LoadPackageFile : LoadPackageFileIntegrationTest
        {
            public LoadPackageFile(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await VerifyOutputAsync(LoadPackageFileDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                await VerifyOutputAsync(LoadPackageFileDir, Step2);
                AssertOnlyInfoLogsOrLess();
            }
        }

        public class LoadPackageFile_WithDelete : LoadPackageFileIntegrationTest
        {
            public LoadPackageFile_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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

                // Set the Last-Modified date for the etag
                var file = new FileInfo(Path.Combine(TestData, "behaviorsample.1.0.0.nupkg"))
                {
                    LastWriteTimeUtc = DateTime.Parse("2021-01-14T18:00:00Z")
                };

                var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
                var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
                var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await VerifyOutputAsync(LoadPackageFile_WithDeleteDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                await VerifyOutputAsync(LoadPackageFile_WithDeleteDir, Step2);
                AssertOnlyInfoLogsOrLess();
            }
        }

        public override bool OnlyLatestLeaves => true;

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageFileTableName });
        }

        public LoadPackageFileIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadPackageFile;

        private async Task VerifyOutputAsync(string testName, string stepName)
        {
            await VerifyWideEntityOutputAsync(
                Options.Value.PackageFileTableName,
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
