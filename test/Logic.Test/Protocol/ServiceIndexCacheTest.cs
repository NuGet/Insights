// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class ServiceIndexCacheTest : BaseLogicIntegrationTest
    {
        [Fact]
        public async Task CachesServiceIndex()
        {
            var urlA = await Target.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var urlB = await Target.GetUrlAsync(ServiceIndexTypes.Catalog);

            var session = Assert.Single(HttpMessageHandlerFactory.RequestAndResponses);
            Assert.Equal(Options.Value.V3ServiceIndex, session.Response.RequestMessage.RequestUri.AbsoluteUri);
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

        public ServiceIndexCacheTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public ServiceIndexCache Target => Host.Services.GetService<ServiceIndexCache>();
    }
}
