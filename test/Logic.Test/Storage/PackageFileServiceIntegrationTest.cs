// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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

            var timestampA = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z");
            var timestampB = timestampA.AddHours(1);

            var leafItem = new CatalogLeafItem
            {
                CommitTimestamp = timestampA,
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                Type = CatalogLeafType.PackageDetails,
            };

            var first = await Target.GetOrUpdateInfoAsync(leafItem);
            var requestCountBefore = HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg"));
            leafItem.CommitTimestamp = timestampB;

            // Act
            var second = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            var requestCountAfter = HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg"));
            Assert.True(requestCountAfter > requestCountBefore);
            Assert.Equal(timestampA, first.CommitTimestamp);
            Assert.Equal(timestampB, second.CommitTimestamp);
        }

        [Fact]
        public async Task DoesNotRepeatHttpRequestsForOlderLeaf()
        {
            // Arrange
            await Target.InitializeAsync();

            var timestampA = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z");
            var timestampB = timestampA.AddHours(-1);

            var leafItem = new CatalogLeafItem
            {
                CommitTimestamp = timestampA,
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                Type = CatalogLeafType.PackageDetails,
            };

            var first = await Target.GetOrUpdateInfoAsync(leafItem);
            var requestCount = HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg"));
            leafItem.CommitTimestamp = timestampB;

            // Act
            var second = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.True(requestCount >= 2); // HEAD, GET, and potential retries on the GET
            Assert.Equal(requestCount, HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg")));
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
                    && LogMessages.Any(m => m.Contains("Metric emitted: FileDownloader.GetZipDirectoryReaderAsync.DurationMs Nupkg")))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = r });
                }

                return Task.FromResult<HttpResponseMessage>(null);
            };

            await Target.InitializeAsync();

            var leafItem = new CatalogLeafItem
            {
                CommitId = "81ef2283-82dd-487f-a274-8731bb87c413",
                CommitTimestamp = DateTimeOffset.Parse("2021-08-13T13:44:11.6356345Z"),
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
                Type = CatalogLeafType.PackageDetails,
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.13.13.44.11/microsoft.codecoverage.16.11.0.json",
            };

            // Act
            var info = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.NotNull(info);
            Assert.True(info.Available);
            var nupkgRequests = HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg"));
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

            var leafItem = new CatalogLeafItem
            {
                CommitId = "dc9945c1-9199-4479-bdea-22a6554d5b4d",
                CommitTimestamp = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z"),
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                Type = CatalogLeafType.PackageDetails,
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.15.01.04.00/newtonsoft.json.9.0.1.json",
            };

            // Act
            var info = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.NotNull(info);
            Assert.True(info.Available);
            var nupkgRequests = HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg"));
            Assert.Empty(nupkgRequests.Where(x => x.Method == HttpMethod.Get && x.Headers.Range is null)); // all range requests
            Assert.Contains(nupkgRequests, x => x.RequestUri.Query.Contains("cache-bust="));
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

            var leafItem = new CatalogLeafItem
            {
                CommitId = "dc9945c1-9199-4479-bdea-22a6554d5b4d",
                CommitTimestamp = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z"),
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                Type = CatalogLeafType.PackageDetails,
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.15.01.04.00/newtonsoft.json.9.0.1.json",
            };

            // Act
            var info = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.NotNull(info);
            Assert.True(info.Available);
            var nupkgRequests = HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg"));
            Assert.NotEmpty(nupkgRequests.Where(x => x.Method == HttpMethod.Get && x.Headers.Range is null));
        }

        [Fact]
        public async Task DoesNotRepeatHttpRequestsForSameLeaf()
        {
            // Arrange
            await Target.InitializeAsync();

            var leafItem = new CatalogLeafItem
            {
                CommitId = "dc9945c1-9199-4479-bdea-22a6554d5b4d",
                CommitTimestamp = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z"),
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                Type = CatalogLeafType.PackageDetails,
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.15.01.04.00/newtonsoft.json.9.0.1.json",
            };

            await Target.GetOrUpdateInfoAsync(leafItem);
            var requestCount = HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg"));

            // Act
            await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.True(requestCount >= 2); // HEAD, GET, and potential retries on the GET
            Assert.Equal(requestCount, HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsolutePath.EndsWith(".nupkg")));
        }

        public PackageFileServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public PackageFileService Target => Host.Services.GetRequiredService<PackageFileService>();
    }
}
