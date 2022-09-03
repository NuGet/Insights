// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class FileDownloaderIntegrationTest : BaseLogicIntegrationTest
    {
        public class TheGetZipDirectoryReaderAsyncMethod : FileDownloaderIntegrationTest
        {
            [Fact]
            public async Task CompletesAllDirectoyHttpRequestsBeforeReturning()
            {
                // Arrange
                var reader = await Target.GetZipDirectoryReaderAsync(
                    "Newtonsoft.Json",
                    "9.0.1",
                    ArtifactFileType.Nupkg,
                    "https://api.nuget.org/v3-flatcontainer/newtonsoft.json/9.0.1/newtonsoft.json.9.0.1.nupkg");
                var requestCount = HttpMessageHandlerFactory.Requests.Count;

                // Act
                await reader.ReadAsync();

                // Act
                Assert.Equal(requestCount, HttpMessageHandlerFactory.Requests.Count);
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
                var requestCount = HttpMessageHandlerFactory.Requests.Count;

                // Act
                await reader.ReadAsync();

                // Act
                Assert.Equal(requestCount, HttpMessageHandlerFactory.Requests.Count);
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
