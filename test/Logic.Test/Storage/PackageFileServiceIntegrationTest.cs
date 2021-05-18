// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class PackageFileServiceIntegrationTest : BaseLogicIntegrationTest
    {
        [Fact]
        public async Task UpdatesDataForNewCatalogLeaf()
        {
            // Arrange
            await Target.InitializeAsync();

            var timestampA = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z");
            var timestampB = timestampA.AddHours(1);

            var leafItem = new CatalogLeafItem
            {
                CommitTimestamp = timestampA,
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                Type = CatalogLeafType.PackageDetails,
            };

            var first = await Target.GetOrUpdateInfoAsync(leafItem);
            var requestCount = HttpMessageHandlerFactory.Requests.Count;
            leafItem.CommitTimestamp = timestampB;

            // Act
            var second = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.Equal(2 * requestCount, HttpMessageHandlerFactory.Requests.Count);
            Assert.Equal(timestampA, first.CommitTimestamp);
            Assert.Equal(timestampB, second.CommitTimestamp);
        }

        [Fact]
        public async Task DoesNotRepeatHttpRequestsForOlderLeaf()
        {
            // Arrange
            await Target.InitializeAsync();

            var timestampA = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z");
            var timestampB = timestampA.AddHours(-1);

            var leafItem = new CatalogLeafItem
            {
                CommitTimestamp = timestampA,
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                Type = CatalogLeafType.PackageDetails,
            };

            var first = await Target.GetOrUpdateInfoAsync(leafItem);
            var requestCount = HttpMessageHandlerFactory.Requests.Count;
            leafItem.CommitTimestamp = timestampB;

            // Act
            var second = await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.Equal(requestCount, HttpMessageHandlerFactory.Requests.Count);
            Assert.Equal(timestampA, first.CommitTimestamp);
            Assert.Equal(timestampA, second.CommitTimestamp);
        }

        [Fact]
        public async Task DoesNotRepeatHttpRequestsForSameLeaf()
        {
            // Arrange
            await Target.InitializeAsync();

            var leafItem = new CatalogLeafItem
            {
                CommitId = "dc9945c1-9199-4479-bdea-22a6554d5b4d",
                CommitTimestamp = DateTimeOffset.Parse("2018-10-15T01:04:00.4615524Z"),
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                Type = CatalogLeafType.PackageDetails,
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.15.01.04.00/newtonsoft.json.9.0.1.json",
            };

            await Target.GetOrUpdateInfoAsync(leafItem);
            var requestCount = HttpMessageHandlerFactory.Requests.Count;

            // Act
            await Target.GetOrUpdateInfoAsync(leafItem);

            // Assert
            Assert.Equal(requestCount, HttpMessageHandlerFactory.Requests.Count);
        }

        public PackageFileServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public PackageFileService Target => Host.Services.GetRequiredService<PackageFileService>();
    }
}
