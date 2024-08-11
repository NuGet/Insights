// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class RedirectResolverTest : BaseLogicIntegrationTest
    {
        [Fact]
        public async Task ResolvesNoRedirects()
        {
            var lastUrl = await Target.FollowRedirectsAsync(new Uri("https://api.nuget.org/v3/index.json"));

            Assert.Equal("https://api.nuget.org/v3/index.json", lastUrl.AbsoluteUri);
        }

        [Fact]
        public async Task ResolvesRedirect()
        {
            var lastUrl = await Target.FollowRedirectsAsync(new Uri("http://nuget.org/"));

            Assert.Equal("https://www.nuget.org/", lastUrl.AbsoluteUri);
        }

        private RedirectResolver Target => Host.Services.GetRequiredService<RedirectResolver>();

        public RedirectResolverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
