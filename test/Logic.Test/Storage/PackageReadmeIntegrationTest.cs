// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class PackageReadmeIntegrationTest : BaseLogicIntegrationTest
    {
        [Fact]
        public async Task ReturnsEmbeddedReadmeContent()
        {
            // Arrange
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                PackageId = "Knapcode.TorSharp",
                PackageVersion = "2.8.1",
                Type = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2021-08-06T00:31:15.51Z"),
            };

            // Act
            var info = await Target.GetOrUpdateInfoAsync(leaf);

            // Assert
            Assert.Single(HttpMessageHandlerFactory.Requests);
            Assert.Equal(ReadmeType.Embedded, info.ReadmeType);
            Assert.NotNull(info.ReadmeBytes);
            Assert.NotEmpty(info.HttpHeaders);
            var readme = Encoding.UTF8.GetString(info.ReadmeBytes.Value.Span);
            Assert.StartsWith("# TorSharp", readme);
        }

        [Fact]
        public async Task ReturnsNoneForNoReadmeContent()
        {
            // Arrange
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                PackageId = "Knapcode.TorSharp",
                PackageVersion = "1.0.0",
                Type = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2021-08-06T00:31:41.2929519Z"),
            };

            // Act
            var info = await Target.GetOrUpdateInfoAsync(leaf);

            // Assert
            Assert.Single(HttpMessageHandlerFactory.Requests);
            Assert.Equal(ReadmeType.None, info.ReadmeType);
        }

        [Fact]
        public async Task ReturnsNoneForNoReadmeContentWithLegacyPattern()
        {
            // This URL pattern is fake.
            ConfigureSettings = x => x.LegacyReadmeUrlPattern = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/legacy-readme";

            // Arrange
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                PackageId = "Knapcode.TorSharp",
                PackageVersion = "1.0.0",
                Type = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2021-08-06T00:31:41.2929519Z"),
            };

            // Act
            var info = await Target.GetOrUpdateInfoAsync(leaf);

            // Assert
            Assert.Equal(2, HttpMessageHandlerFactory.Requests.Count);
            Assert.Equal(
                "https://api.nuget.org/v3-flatcontainer/knapcode.torsharp/1.0.0/legacy-readme",
                HttpMessageHandlerFactory.Requests.ElementAt(1).RequestUri.AbsoluteUri);
            Assert.Equal(ReadmeType.None, info.ReadmeType);
        }

        [Fact]
        public async Task ReturnsNoneForDeletedPackage()
        {
            // Arrange
            await Target.InitializeAsync();
            var leaf = new CatalogLeafItem
            {
                PackageId = "nuget.platform",
                PackageVersion = "1.0.0",
                Type = CatalogLeafType.PackageDelete,
                CommitTimestamp = DateTimeOffset.Parse("2017-11-08T17:42:28.5677911"),
            };

            // Act
            var info = await Target.GetOrUpdateInfoAsync(leaf);

            // Assert
            Assert.Empty(HttpMessageHandlerFactory.Requests);
            Assert.Equal(ReadmeType.None, info.ReadmeType);
        }

        public PackageReadmeService Target => Host.Services.GetRequiredService<PackageReadmeService>();

        public PackageReadmeIntegrationTest(
            ITestOutputHelper output,
            DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
