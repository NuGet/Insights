// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MessagePack;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.LoadPackageReadme
{
    public class LoadPackageReadmeIntegrationTest : BaseCatalogScanIntegrationTest
    {
        public const string LoadPackageReadmeDir = nameof(LoadPackageReadme);
        public const string LoadPackageReadme_WithDeleteDir = nameof(LoadPackageReadme_WithDelete);

        [Fact]
        public async Task LoadPackageReadme()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2022-03-14T23:05:39.6122305Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2022-03-14T23:06:07.7549588Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2022-03-14T23:06:36.1633247Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(LoadPackageReadmeDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(LoadPackageReadmeDir, Step2);
        }

        [Fact]
        public async Task LoadPackageReadme_WithDelete()
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
            await AssertOutputAsync(LoadPackageReadme_WithDeleteDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(LoadPackageReadme_WithDeleteDir, Step2);
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageReadmeTableName });
        }

        public LoadPackageReadmeIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadPackageReadme;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        private async Task AssertOutputAsync(string testName, string stepName)
        {
            Assert.Empty(HttpMessageHandlerFactory.Responses.Where(x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith(".nupkg", StringComparison.Ordinal)));
            Assert.NotEmpty(HttpMessageHandlerFactory.Responses.Where(x => x.RequestMessage.RequestUri.AbsoluteUri.EndsWith("/readme", StringComparison.Ordinal)));

            await AssertWideEntityOutputAsync(
                Options.Value.PackageReadmeTableName,
                Path.Combine(testName, stepName),
                stream =>
                {
                    var entity = MessagePackSerializer.Deserialize<PackageReadmeService.PackageReadmeInfoVersions>(stream, NuGetInsightsMessagePack.Options);

                    string readmeHash = null;
                    SortedDictionary<string, List<string>> httpHeaders = null;

                    if (entity.V1.ReadmeType != ReadmeType.None)
                    {
                        using var algorithm = SHA256.Create();
                        readmeHash = algorithm.ComputeHash(entity.V1.ReadmeBytes.ToArray()).ToLowerHex();
                        httpHeaders = NormalizeHeaders(entity.V1.HttpHeaders, ignore: new[] { "Content-MD5" } );
                    }

                    return new
                    {
                        entity.V1.ReadmeType,
                        entity.V1.CommitTimestamp,
                        HttpHeaders = httpHeaders,
                        ReadmeHash = readmeHash,
                    };
                });
        }
    }
}
