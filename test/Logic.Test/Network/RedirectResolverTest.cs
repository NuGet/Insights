// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class RedirectResolverTest : BaseLogicIntegrationTest
    {
        [Fact]
        public async Task ResolvesNoRedirects()
        {
            var lastUrl = await Target.FollowRedirectsAsync("https://httpbin.org/get");

            Assert.Equal("https://httpbin.org/get", lastUrl.AbsoluteUri);
        }

        [Fact]
        public async Task ResolvesMultipleRedirects()
        {
            var lastUrl = await Target.FollowRedirectsAsync("https://httpbin.org/redirect/3");

            Assert.Equal("https://httpbin.org/get", lastUrl.AbsoluteUri);
        }

        private RedirectResolver Target => Host.Services.GetRequiredService<RedirectResolver>();

        public RedirectResolverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
