// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageReadmeToCsv
{
    public class PackageReadmeToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageReadmeToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public ICatalogLeafToCsvDriver<PackageReadme> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageReadme>>();

        [Fact]
        public async Task HandlesEmbeddedReadme()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                PackageId = "Knapcode.TorSharp",
                PackageVersion = "2.8.1",
                Type = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2021-08-06T00:31:15.51Z"),
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.06.00.31.41/knapcode.torsharp.2.8.1.json",
            };

            // Act
            var result = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            // Assert
        }
    }
}
