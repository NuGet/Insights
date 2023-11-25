// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using Knapcode.MiniZip;

namespace NuGet.Insights
{
    public class FileDownloaderIntegrationTest : BaseLogicIntegrationTest
    {
        public class TheGetZipDirectoryReaderAsyncMethod : FileDownloaderIntegrationTest
        {
            [Fact]
            public async Task ReturnsPropertiesForFullDownload()
            {
                // Arrange
                var url = $"http://localhost/{TestInput}/deltax.1.0.0.nupkg";
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.RequestUri.GetLeftPart(UriPartial.Path) == url)
                    {
                        if (r.Method != HttpMethod.Get || r.Headers.Range is not null)
                        {
                            return new HttpResponseMessage(HttpStatusCode.InternalServerError) { RequestMessage = r };
                        }

                        var newReq = Clone(r);
                        var builder = new UriBuilder(r.RequestUri);
                        builder.Path += ".testdata";
                        newReq.RequestUri = builder.Uri;
                        var response = await TestDataHttpClient.SendAsync(newReq);
                        response.EnsureSuccessStatusCode();
                        return response;
                    }

                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                };

                // Act
                var reader = await Target.GetZipDirectoryReaderAsync("DeltaX", "1.0.0", ArtifactFileType.Nupkg, url);

                // Assert
                var directory = await reader.ReadAsync();
                Assert.Contains("DeltaX.nuspec", directory.Entries.Select(x => x.GetName()));
                Assert.Equal(4, reader.Properties.Count);
                Assert.Contains("Content-Length", reader.Properties.Select(x => x.Key));
                Assert.Equal("12830", reader.Properties["Content-Length"].Single());

                Assert.Contains(LogMessages, x => x.Contains("Trying again with a full download.", StringComparison.Ordinal));
                Assert.Equal(7, HttpMessageHandlerFactory.Responses.Count());
                Assert.Equal(3, HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.Method == HttpMethod.Head
                    && x.RequestMessage.RequestUri.AbsoluteUri == url));
                Assert.Equal(3, HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.Method == HttpMethod.Head
                    && x.RequestMessage.RequestUri.GetLeftPart(UriPartial.Path) == url
                    && x.RequestMessage.RequestUri.Query.Contains("cache-bust=", StringComparison.Ordinal)));
                Assert.Equal(1, HttpMessageHandlerFactory.Responses.Count(x => x.RequestMessage.Method == HttpMethod.Get
                    && x.RequestMessage.RequestUri.GetLeftPart(UriPartial.Path).StartsWith(url, StringComparison.Ordinal)
                    && x.RequestMessage.RequestUri.Query.Contains("cache-bust=", StringComparison.Ordinal)));
            }

            [Fact]
            public async Task CompletesAllDirectoyHttpRequestsBeforeReturning()
            {
                // Arrange
                var reader = await Target.GetZipDirectoryReaderAsync(
                    "Newtonsoft.Json",
                    "9.0.1",
                    ArtifactFileType.Nupkg,
                    "https://api.nuget.org/v3-flatcontainer/newtonsoft.json/9.0.1/newtonsoft.json.9.0.1.nupkg");
                var requestCount = HttpMessageHandlerFactory.Responses.Count();

                // Act
                await reader.ReadAsync();

                // Act
                Assert.Equal(requestCount, HttpMessageHandlerFactory.Responses.Count());
            }

            [Fact]
            public async Task CompletesAllDirectoyHttpRequestsBeforeReturningWhenRangeRequestsFail()
            {
                // Arrange
                HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Headers.Range != null)
                    {
                        return new HttpResponseMessage(HttpStatusCode.BadRequest);
                    }

                    return await b(r, t);
                };

                var reader = await Target.GetZipDirectoryReaderAsync(
                    "Newtonsoft.Json",
                    "9.0.1",
                    ArtifactFileType.Nupkg,
                    "https://api.nuget.org/v3-flatcontainer/newtonsoft.json/9.0.1/newtonsoft.json.9.0.1.nupkg");
                var requestCount = HttpMessageHandlerFactory.Responses.Count();

                // Act
                await reader.ReadAsync();

                // Act
                Assert.Equal(requestCount, HttpMessageHandlerFactory.Responses.Count());
            }

            public TheGetZipDirectoryReaderAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public FileDownloaderIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public FileDownloader Target => Host.Services.GetRequiredService<FileDownloader>();
    }
}
