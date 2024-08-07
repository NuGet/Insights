// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using MessagePack;

namespace NuGet.Insights.Worker.LoadPackageManifest
{
    public class LoadPackageManifestIntegrationTest : BaseCatalogScanIntegrationTest
    {
        public const string LoadPackageManifestDir = nameof(LoadPackageManifest);
        public const string LoadPackageManifest_WithDeleteDir = nameof(LoadPackageManifest_WithDelete);

        [Fact]
        public async Task LoadPackageManifest()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(LoadPackageManifestDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(LoadPackageManifestDir, Step2);
        }

        [Fact]
        public async Task LoadPackageManifest_WithDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(LoadPackageManifest_WithDeleteDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(LoadPackageManifest_WithDeleteDir, Step2);
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageManifestTableName });
        }

        public LoadPackageManifestIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadPackageManifest;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        private async Task AssertOutputAsync(string testName, string stepName)
        {
            Assert.DoesNotContain(HttpMessageHandlerFactory.Responses, x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith(".nupkg", StringComparison.Ordinal));
            Assert.Contains(HttpMessageHandlerFactory.Responses, x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith(".nuspec", StringComparison.Ordinal));

            await AssertWideEntityOutputAsync(
                Options.Value.PackageManifestTableName,
                Path.Combine(testName, stepName),
                stream =>
                {
                    var entity = MessagePackSerializer.Deserialize<PackageManifestService.PackageManifestInfoVersions>(stream, NuGetInsightsMessagePack.Options);

                    string manifestHash = null;
                    SortedDictionary<string, List<string>> httpHeaders = null;

                    if (entity.V1.Available)
                    {
                        using var algorithm = SHA256.Create();
                        manifestHash = algorithm.ComputeHash(entity.V1.ManifestBytes.ToArray()).ToLowerHex();
                        httpHeaders = NormalizeHeaders(entity.V1.HttpHeaders);
                    }

                    return new
                    {
                        entity.V1.Available,
                        entity.V1.CommitTimestamp,
                        HttpHeaders = httpHeaders,
                        ManifestHash = manifestHash,
                    };
                });
        }
    }
}
