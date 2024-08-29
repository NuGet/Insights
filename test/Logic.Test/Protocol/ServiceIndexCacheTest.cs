// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Caching.Memory;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Insights
{
    public class ServiceIndexCacheTest : IDisposable
    {
        [Fact]
        public async Task CachesServiceIndex()
        {
            var urlA = await Target.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var urlB = await Target.GetUrlAsync(ServiceIndexTypes.RegistrationSemVer2);

            var session = Assert.Single(HttpMessageHandlerFactory.RequestAndResponses);
            Assert.Equal(Settings.V3ServiceIndex, session.Response.RequestMessage.RequestUri.AbsoluteUri);
        }

        [Theory]
        [MemberData(nameof(ServiceTypeTestData))]
        public async Task NuGetOrgCanReturnAllDeclaredServiceTypes(string type)
        {
            var urls = await Target.GetUrlsAsync(type);

            Assert.NotEmpty(urls);
            var session = Assert.Single(HttpMessageHandlerFactory.RequestAndResponses);
        }

        public static IEnumerable<object[]> ServiceTypeTestData => typeof(ServiceIndexTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null))
            .Order()
            .Select(t => new object[] { t })
            .ToList();

        [Fact]
        public async Task RetriesOnFailure()
        {
            var requestCount = 0;
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                if (Interlocked.Increment(ref requestCount) == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                    {
                        RequestMessage = r
                    };
                }

                return await b(r, t);
            };

            var url = await Target.GetUrlAsync(ServiceIndexTypes.FlatContainer);

            Assert.Equal(2, HttpMessageHandlerFactory.RequestAndResponses.Count);
            Assert.Equal(HttpStatusCode.TooManyRequests, HttpMessageHandlerFactory.RequestAndResponses.First().Response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, HttpMessageHandlerFactory.RequestAndResponses.Last().Response.StatusCode);
        }

        public ServiceIndexCacheTest(ITestOutputHelper output)
        {
            Output = output;
            RealHttpClientHandler = new HttpClientHandler();
            HttpMessageHandlerFactory = new TestHttpMessageHandlerFactory();
            HttpMessageHandlerFactory.OnSendAsync = (r, b, t) => b(r, t);
            Settings = new NuGetInsightsSettings
            {
                V3ServiceIndex = "https://api.nuget.org/v3/index.json",
            };
            Options = new Mock<IOptions<NuGetInsightsSettings>>();
            Options.Setup(x => x.Value).Returns(() => Settings);
            HttpSource = new HttpSource(
                new PackageSource(Settings.V3ServiceIndex),
                () =>
                {
                    var httpMessageHandler = HttpMessageHandlerFactory.Create();
                    httpMessageHandler.InnerHandler = RealHttpClientHandler;
                    var resource = new HttpMessageHandlerResource(httpMessageHandler);
                    return Task.FromResult<HttpHandlerResource>(resource);
                },
                NullThrottle.Instance);
            MemoryCache = new MemoryCache(
                Microsoft.Extensions.Options.Options.Create(new MemoryCacheOptions()),
                Output.GetLoggerFactory());
            Target = new ServiceIndexCache(
                HttpSource,
                MemoryCache,
                Options.Object,
                Output.GetLogger<ServiceIndexCache>());
        }

        public void Dispose()
        {
            RealHttpClientHandler.Dispose();
        }

        public ITestOutputHelper Output { get; }
        public HttpClientHandler RealHttpClientHandler { get; }
        public TestHttpMessageHandlerFactory HttpMessageHandlerFactory { get; }
        public NuGetInsightsSettings Settings { get; }
        public Mock<IOptions<NuGetInsightsSettings>> Options { get; }
        public HttpSource HttpSource { get; }
        public MemoryCache MemoryCache { get; }
        public ServiceIndexCache Target { get; }
    }
}
