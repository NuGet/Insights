// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NuGet.Insights;
using NuGet.Insights.Website;
using NuGet.Insights.Worker;

namespace Website.Test
{
    public class BaseWebsiteIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddNuGetInsightsWebsite();

                    // don't run hosted services in tests
                    serviceCollection.RemoveAll<IHostedService>();
                });
        }

        public BaseWebsiteIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
