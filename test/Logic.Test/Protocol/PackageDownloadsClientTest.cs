// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class PackageDownloadsClientTest
    {
        [Fact]
        public async Task RejectsOldData()
        {
            var lastModified = DateTimeOffset.Parse("2022-01-23T16:15:00Z");
            Settings.DownloadsV1AgeLimit = TimeSpan.FromHours(1);

            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                await Task.Yield();

                if (Settings.DownloadsV1Urls.Contains(r.RequestUri.AbsoluteUri))
                {
                    var json = JsonSerializer.Serialize(Array.Empty<object[]>());

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { ETag = new EntityTagHeaderValue("\"my-etag\"", isWeak: true) },
                        Content = new StringContent(json)
                        {
                            Headers = { LastModified = lastModified },
                        },
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.GetAsync());
            Assert.Equal(
                $"The last modified downloads.v1.json URL is {Settings.DownloadsV1Urls.Single()} " +
                $"(modified on {lastModified:O}) " +
                $"but this is older than the age limit of 01:00:00. " +
                $"Check for stale data or bad configuration.", ex.Message);
        }

        [Fact]
        public async Task ReturnsNewestData()
        {
            Settings.DownloadsV1Urls = new List<string>
            {
                "https://api.example.com/a/downloads.v1.json",
                "https://api.example.com/b/downloads.v1.json",
                "https://api.example.com/c/downloads.v1.json",
            };

            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                await Task.Yield();

                if (r.RequestUri.AbsoluteUri == "https://api.example.com/a/downloads.v1.json")
                {
                    var json = JsonSerializer.Serialize(new object[][]
                    {
                        new object[] { "Newtonsoft.Json", new object[] { "10.0.1", 100 } },
                    });

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { ETag = new EntityTagHeaderValue("\"my-etag\"", isWeak: true) },
                        Content = new StringContent(json)
                        {
                            Headers = { LastModified = DateTimeOffset.Parse("2022-01-23T16:15:00Z") },
                        },
                    };
                }

                if (r.RequestUri.AbsoluteUri == "https://api.example.com/b/downloads.v1.json")
                {
                    var json = JsonSerializer.Serialize(new object[][]
                    {
                        new object[] { "Newtonsoft.Json", new object[] { "10.0.1", 101 } },
                    });

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { ETag = new EntityTagHeaderValue("\"my-etag\"", isWeak: true) },
                        Content = new StringContent(json)
                        {
                            Headers = { LastModified = DateTimeOffset.Parse("2022-01-25T16:15:00Z") },
                        },
                    };
                }

                if (r.RequestUri.AbsoluteUri == "https://api.example.com/c/downloads.v1.json")
                {
                    var json = JsonSerializer.Serialize(new object[][]
                    {
                        new object[] { "Newtonsoft.Json", new object[] { "10.0.1", 102 } },
                    });

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { ETag = new EntityTagHeaderValue("\"my-etag\"", isWeak: true) },
                        Content = new StringContent(json)
                        {
                            Headers = { LastModified = DateTimeOffset.Parse("2022-01-24T16:15:00Z") },
                        },
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
            };

            await using var data = await Target.GetAsync();

            var entries = await data.Entries.ToArrayAsync();
            Assert.Equal("https://api.example.com/b/downloads.v1.json", data.Url);
            Assert.Equal(
                new[]
                {
                    new PackageDownloads("Newtonsoft.Json", "10.0.1", 101),
                },
                entries);
        }

        [Fact]
        public async Task HandlesIdWithNoVersion()
        {
            var lastModified = DateTimeOffset.Parse("2022-01-23T16:15:00Z");
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                await Task.Yield();

                if (Settings.DownloadsV1Urls.Contains(r.RequestUri.AbsoluteUri))
                {
                    var json = JsonSerializer.Serialize(new object[][]
                    {
                        new object[] { "Newtonsoft.Json", new object[] { "10.0.1", 100 } },
                        new object[] { "NuGet.Versioning" },
                        new object[] { "NuGet.Frameworks", new object[] { "1.0", 200 } },
                    });

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { ETag = new EntityTagHeaderValue("\"my-etag\"", isWeak: true) },
                        Content = new StringContent(json)
                        {
                            Headers = { LastModified = lastModified },
                        },
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
            };

            await using var data = await Target.GetAsync();

            var entries = await data.Entries.ToArrayAsync();
            Assert.Equal(
                new[]
                {
                    new PackageDownloads("Newtonsoft.Json", "10.0.1", 100),
                    new PackageDownloads("NuGet.Frameworks", "1.0", 200),
                },
                entries);
        }

        [Fact]
        public async Task HandlesEmptyArray()
        {
            var lastModified = DateTimeOffset.Parse("2022-01-23T16:15:00Z");
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                await Task.Yield();

                if (Settings.DownloadsV1Urls.Contains(r.RequestUri.AbsoluteUri))
                {
                    var json = JsonSerializer.Serialize(Array.Empty<object>());

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { ETag = new EntityTagHeaderValue("\"my-etag\"", isWeak: true) },
                        Content = new StringContent(json)
                        {
                            Headers = { LastModified = lastModified },
                        },
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
            };

            await using var data = await Target.GetAsync();

            Assert.Empty(await data.Entries.ToArrayAsync());
        }

        [Fact]
        public async Task ParsesDownloads()
        {
            var lastModified = DateTimeOffset.Parse("2022-01-23T16:15:00Z");
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                await Task.Yield();

                if (Settings.DownloadsV1Urls.Contains(r.RequestUri.AbsoluteUri))
                {
                    var json = JsonSerializer.Serialize(new object[][]
                    {
                        new object[]
                        {
                            "Newtonsoft.Json",
                            new object[] { "10.0.1", 100 },
                            new object[] { "9.0.1", 200 },
                        },
                        new object[]
                        {
                            "NuGet.Versioning",
                            new object[] { "3.4.3", 300 },
                            new object[] { "6.0.0", 400 },
                        },
                        new object[]
                        {
                            "NuGet.Frameworks",
                            new object[] { "1.0", 500 },
                        },
                        new object[]
                        {
                            "Newtonsoft.Json",
                            new object[] { "10.0.1", 600 },
                            new object[] { "8.0.1", 700 },
                        },
                    });

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { ETag = new EntityTagHeaderValue("\"my-etag\"", isWeak: true) },
                        Content = new StringContent(json)
                        {
                            Headers = { LastModified = lastModified },
                        },
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
            };

            await using var data = await Target.GetAsync();

            Assert.Equal("W/\"my-etag\"", data.ETag);
            Assert.Equal(lastModified, data.AsOfTimestamp);
            Assert.Equal(Settings.DownloadsV1Urls.Single(), data.Url);
            var entries = await data.Entries.ToArrayAsync();
            Assert.Equal(
                new[]
                {
                    new PackageDownloads("Newtonsoft.Json", "10.0.1", 100),
                    new PackageDownloads("Newtonsoft.Json", "9.0.1", 200),
                    new PackageDownloads("NuGet.Versioning", "3.4.3", 300),
                    new PackageDownloads("NuGet.Versioning", "6.0.0", 400),
                    new PackageDownloads("NuGet.Frameworks", "1.0", 500),
                    new PackageDownloads("Newtonsoft.Json", "10.0.1", 600),
                    new PackageDownloads("Newtonsoft.Json", "8.0.1", 700),
                },
                entries);
            Assert.Equal(1, Throttle.CurrentCount);
        }

        public PackageDownloadsClientTest(ITestOutputHelper output)
        {
            Settings = new NuGetInsightsSettings
            {
                DownloadsV1Urls = new List<string> { "https://api.example.com/downloads.v1.json" },
                DownloadsV1AgeLimit = TimeSpan.MaxValue,
            };
            Options = new Mock<IOptions<NuGetInsightsSettings>>();
            Options.Setup(x => x.Value).Returns(() => Settings);
            HttpMessageHandlerFactory = new TestHttpMessageHandlerFactory();
            HttpClient = new HttpClient(HttpMessageHandlerFactory.Create());
            Throttle = new SemaphoreSlimThrottle(new SemaphoreSlim(1));
            TelemetryClient = output.GetTelemetryClient();
            StorageClient = new BlobStorageJsonClient(HttpClient, Throttle, TelemetryClient);
            Target = new PackageDownloadsClient(StorageClient, Options.Object);
        }

        public NuGetInsightsSettings Settings { get; }
        public Mock<IOptions<NuGetInsightsSettings>> Options { get; }
        public TestHttpMessageHandlerFactory HttpMessageHandlerFactory { get; }
        public HttpClient HttpClient { get; }
        public SemaphoreSlimThrottle Throttle { get; }
        public ITelemetryClient TelemetryClient { get; }
        public BlobStorageJsonClient StorageClient { get; }
        public PackageDownloadsClient Target { get; }
    }
}
