// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageCompatibilityToCsv
{
    public class PackageCompatibilityToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageCompatibilityToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            FailFastLogLevel = LogLevel.None;
            AssertLogLevel = LogLevel.None;
        }

        public ICatalogLeafToCsvDriver<PackageCompatibility> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageCompatibility>>();

        [Fact]
        public async Task InvalidPortable()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.30.01.37.28/chatwork.api.0.3.2.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Chatwork.Api",
                PackageVersion = "0.3.2",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.True(record.HasError);
            Assert.Equal(PackageCompatibilityResultType.Available, record.ResultType);
        }
    }
}
