// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class PackageFileServiceIntegrationTest : BaseLogicIntegrationTest
    {
        [Fact]
        public async Task UpdatesDataForNewCatalogLeaf()
        {
            // Arrange
            await Target.InitializeAsync();

            var timestampA = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z", CultureInfo.InvariantCulture);
            var timestampB = timestampA.AddHours(1);

            var leafItem = new PackageIdentityCommit
            {
                CommitTimestamp = timestampA,
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                LeafType = CatalogLeafType.PackageDetails,
            };

            var first = await Target.GetOrUpdateInfoAsync(leafItem);
            var requestCountBefore = HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg", StringComparison.Ordinal));
            leafItem.CommitTimestamp = timestampB;

            // Act
            var second = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            var requestCountAfter = HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg", StringComparison.Ordinal));
            Assert.True(requestCountAfter > requestCountBefore);
            Assert.Equal(timestampA, first.CommitTimestamp);
            Assert.Equal(timestampB, second.CommitTimestamp);
        }

        [Fact]
        public async Task DoesNotRepeatHttpRequestsForOlderLeaf()
        {
            // Arrange
            await Target.InitializeAsync();

            var timestampA = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z", CultureInfo.InvariantCulture);
            var timestampB = timestampA.AddHours(-1);

            var leafItem = new PackageIdentityCommit
            {
                CommitTimestamp = timestampA,
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                LeafType = CatalogLeafType.PackageDetails,
            };

            var first = await Target.GetOrUpdateInfoAsync(leafItem);
            var requestCount = HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg", StringComparison.Ordinal));
            leafItem.CommitTimestamp = timestampB;

            // Act
            var second = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.True(requestCount >= 2, $"There should be at least 2 requests. Actual: {requestCount}."); // HEAD and GET allowing for retries
            Assert.Equal(requestCount, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg", StringComparison.Ordinal)));
            Assert.Equal(timestampA, first.CommitTimestamp);
            Assert.Equal(timestampA, second.CommitTimestamp);
        }

        [Fact]
        public async Task HandlesFailureWhenFetchingTheSignatureFile()
        {
            // Arrange
            HttpMessageHandlerFactory.OnSendAsync = (r, b, t) =>
            {
                if (r.Headers.Range is not null
                    && TelemetryClient.Metrics.TryGetValue(new("FileDownloader.GetZipDirectoryReaderAsync.DurationMs", "ArtifactFileType", "DownloadMode"), out var metric)
                    && metric.MetricValues.Any(x => x.DimensionValues.Contains("Nupkg")))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = r });
                }

                return Task.FromResult<HttpResponseMessage>(null);
            };

            await Target.InitializeAsync();

            var leafItem = new PackageIdentityCommit
            {
                CommitTimestamp = DateTimeOffset.Parse("2021-08-13T13:44:11.6356345Z", CultureInfo.InvariantCulture),
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
                LeafType = CatalogLeafType.PackageDetails,
            };

            // Act
            var info = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.NotNull(info);
            Assert.True(info.Available);
            var nupkgRequests = HttpMessageHandlerFactory.SuccessRequests.Where(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg", StringComparison.Ordinal));
            Assert.NotEmpty(nupkgRequests.Where(x => x.Method == HttpMethod.Get && x.Headers.Range is null));
        }

        [Fact]
        public async Task RetriesWithCacheBust()
        {
            // Arrange
            HttpMessageHandlerFactory.OnSendAsync = (r, b, t) =>
            {
                if (r.Headers.Range is not null && string.IsNullOrEmpty(r.RequestUri.Query))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = r });
                }

                return Task.FromResult<HttpResponseMessage>(null);
            };

            await Target.InitializeAsync();

            var leafItem = new PackageIdentityCommit
            {
                CommitTimestamp = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z", CultureInfo.InvariantCulture),
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                LeafType = CatalogLeafType.PackageDetails,
            };

            // Act
            var info = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.NotNull(info);
            Assert.True(info.Available);
            var nupkgRequests = HttpMessageHandlerFactory.SuccessRequests.Where(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg", StringComparison.Ordinal));
            Assert.Contains(nupkgRequests, x => x.RequestUri.Query.Contains("cache-bust=", StringComparison.Ordinal));
        }

        [Fact]
        public async Task RetriesWithFullDownload()
        {
            // Arrange
            HttpMessageHandlerFactory.OnSendAsync = (r, b, t) =>
            {
                if (r.Headers.Range is not null)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = r });
                }

                return Task.FromResult<HttpResponseMessage>(null);
            };

            await Target.InitializeAsync();

            var leafItem = new PackageIdentityCommit
            {
                CommitTimestamp = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z", CultureInfo.InvariantCulture),
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                LeafType = CatalogLeafType.PackageDetails,
            };

            // Act
            var info = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.NotNull(info);
            Assert.True(info.Available);
            var nupkgRequests = HttpMessageHandlerFactory.SuccessRequests.Where(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg", StringComparison.Ordinal));
            Assert.NotEmpty(nupkgRequests.Where(x => x.Method == HttpMethod.Get && x.Headers.Range is null));
        }

        [Fact]
        public async Task DoesNotRepeatHttpRequestsForSameLeaf()
        {
            // Arrange
            await Target.InitializeAsync();

            var leafItem = new PackageIdentityCommit
            {
                CommitTimestamp = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z", CultureInfo.InvariantCulture),
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                LeafType = CatalogLeafType.PackageDetails,
            };

            await Target.GetOrUpdateInfoAsync(leafItem);
            var requestCount = HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg", StringComparison.Ordinal));

            // Act
            await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.True(requestCount >= 2); // HEAD, GET, and potential retries on the GET
            Assert.Equal(requestCount, HttpMessageHandlerFactory.SuccessRequests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg", StringComparison.Ordinal)));
        }

        public PackageFileServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public PackageFileService Target => Host.Services.GetRequiredService<PackageFileService>();
    }
}
