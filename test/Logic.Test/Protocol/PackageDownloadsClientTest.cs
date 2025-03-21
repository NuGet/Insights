// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageDownloadsClientTest
    {
        [Fact]
        public async Task RejectsOldData()
        {
            var lastModified = DateTimeOffset.Parse("2022-01-23T16:15:00Z", CultureInfo.InvariantCulture);
            Settings.DownloadsV1AgeLimit = TimeSpan.FromHours(1);

            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                await Task.Yield();

                if (Settings.DownloadsV1Urls.Contains(r.RequestUri.AbsoluteUri))
                {
                    var json = JsonSerializer.Serialize(Array.Empty<object[]>());

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        RequestMessage = r,
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
                        RequestMessage = r,
                        Headers = { ETag = new EntityTagHeaderValue("\"my-etag\"", isWeak: true) },
                        Content = new StringContent(json)
                        {
                            Headers = { LastModified = DateTimeOffset.Parse("2022-01-23T16:15:00Z", CultureInfo.InvariantCulture) },
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
                        RequestMessage = r,
                        Headers = { ETag = new EntityTagHeaderValue("\"my-etag\"", isWeak: true) },
                        Content = new StringContent(json)
                        {
                            Headers = { LastModified = DateTimeOffset.Parse("2022-01-25T16:15:00Z", CultureInfo.InvariantCulture) },
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
                        RequestMessage = r,
                        Headers = { ETag = new EntityTagHeaderValue("\"my-etag\"", isWeak: true) },
                        Content = new StringContent(json)
                        {
                            Headers = { LastModified = DateTimeOffset.Parse("2022-01-24T16:15:00Z", CultureInfo.InvariantCulture) },
                        },
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
            };

            await using var data = await Target.GetAsync();

            var pages = await data.Pages.Select(x => x.ToList()).ToArrayAsync();
            Assert.Equal("https://api.example.com/b/downloads.v1.json", data.Url.AbsoluteUri);
            Assert.Equal(
                new[]
                {
                    new PackageDownloads("Newtonsoft.Json", "10.0.1", 101),
                },
                pages.SelectMany(x => x).ToArray());
        }

        [Fact]
        public async Task HandlesIdWithNoVersion()
        {
            var lastModified = DateTimeOffset.Parse("2022-01-23T16:15:00Z", CultureInfo.InvariantCulture);
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
                        RequestMessage = r,
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

            var pages = await data.Pages.Select(x => x.ToList()).ToArrayAsync();
            Assert.Equal(
                new[]
                {
                    new PackageDownloads("Newtonsoft.Json", "10.0.1", 100),
                    new PackageDownloads("NuGet.Frameworks", "1.0", 200),
                },
                pages.SelectMany(x => x).ToArray());
        }

        [Fact]
        public async Task HandlesEmptyArray()
        {
            var lastModified = DateTimeOffset.Parse("2022-01-23T16:15:00Z", CultureInfo.InvariantCulture);
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                await Task.Yield();

                if (Settings.DownloadsV1Urls.Contains(r.RequestUri.AbsoluteUri))
                {
                    var json = JsonSerializer.Serialize(Array.Empty<object>());

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        RequestMessage = r,
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

            Assert.Empty(await data.Pages.ToArrayAsync());
        }

        [Fact]
        public async Task ParsesDownloads()
        {
            var lastModified = DateTimeOffset.Parse("2022-01-23T16:15:00Z", CultureInfo.InvariantCulture);
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
                        RequestMessage = r,
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
            Assert.Equal(Settings.DownloadsV1Urls.Single(), data.Url.AbsoluteUri);
            var pages = await data.Pages.Select(x => x.ToList()).ToArrayAsync();
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
                pages.SelectMany(x => x).ToArray());
            Assert.Equal(1, Throttle.CurrentCount);
        }

        public PackageDownloadsClientTest(ITestOutputHelper output)
        {
            Settings = new NuGetInsightsSettings
            {
                DownloadsV1Urls = new List<string> { "https://api.example.com/downloads.v1.json" },
                DownloadsV1AgeLimit = TimeSpan.MaxValue,
            }.WithTestStorageSettings();
            Options = new Mock<IOptions<NuGetInsightsSettings>>();
            Options.Setup(x => x.Value).Returns(() => Settings);
            HttpMessageHandlerFactory = new TestHttpMessageHandlerFactory(output.GetLoggerFactory());
            HttpClient = new HttpClient(HttpMessageHandlerFactory.Create());
            Throttle = new SemaphoreSlimThrottle(new SemaphoreSlim(1));
            RedirectResolver = new RedirectResolver(() => HttpClient, output.GetLogger<RedirectResolver>());
            TelemetryClient = output.GetTelemetryClient();
            ServiceClientFactory = new ServiceClientFactory(Options.Object, TelemetryClient, output.GetLoggerFactory());
            StorageClient = new ExternalBlobStorageClient(
                () => HttpClient,
                RedirectResolver,
                ServiceClientFactory,
                Throttle,
                TelemetryClient,
                Options.Object,
                output.GetLogger<ExternalBlobStorageClient>());
            Target = new PackageDownloadsClient(StorageClient, Options.Object);
        }

        public NuGetInsightsSettings Settings { get; }
        public Mock<IOptions<NuGetInsightsSettings>> Options { get; }
        public TestHttpMessageHandlerFactory HttpMessageHandlerFactory { get; }
        public HttpClient HttpClient { get; }
        public SemaphoreSlimThrottle Throttle { get; }
        public RedirectResolver RedirectResolver { get; }
        public ServiceClientFactory ServiceClientFactory { get; }
        public ITelemetryClient TelemetryClient { get; }
        public ExternalBlobStorageClient StorageClient { get; }
        public PackageDownloadsClient Target { get; }
    }
}
