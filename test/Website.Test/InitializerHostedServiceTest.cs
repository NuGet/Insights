// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights;
using NuGet.Insights.Website;

namespace Website.Test
{
    public class InitializerHostedServiceTest : BaseWebsiteIntegrationTest
    {
        [Fact]
        public async Task InitializationIsCached()
        {
            // Arrange
            var initializer = Host.Services.GetRequiredService<InitializerHostedService>();
            await initializer.InitializeAsync(warmUp: false, CancellationToken.None);
            if (!LogicTestSettings.UseMemoryStorage)
            {
                Assert.NotEmpty(HttpMessageHandlerFactory.Requests);
            }
            HttpMessageHandlerFactory.Requests.Clear();

            // Act
            await initializer.InitializeAsync(warmUp: false, CancellationToken.None);

            // Assert
            Assert.Empty(HttpMessageHandlerFactory.Requests);
        }

        public InitializerHostedServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
