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
            Assert.Equal(PackageCompatibilityResultType.Available, record.ResultType);
            Assert.True(record.HasError);
            Assert.False(record.DoesNotRoundTrip);
            Assert.Null(record.NuspecReader);
            Assert.Null(record.NuGetGallery);
        }

        [Fact]
        public async Task InvalidFramework()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.07.17.13.16.14/lagovista.useradmin.rest.3.0.1522.906.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "LagoVista.UserAdmin.Rest",
                PackageVersion = "3.0.1522.906",
            };

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageCompatibilityResultType.Available, record.ResultType);
            Assert.False(record.HasError);
            Assert.True(record.DoesNotRoundTrip);
            Assert.Equal("[\"net5.0\"]", record.NuspecReader);
            Assert.Equal("[\"net5.0\",\"unsupported\"]", record.NuGetGallery);
        }
    }
}
