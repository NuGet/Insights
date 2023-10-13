// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class SymbolPackageFileServiceIntegrationTest : BaseLogicIntegrationTest
    {
        public class TheGetOrUpdateInfoAsyncMethod : SymbolPackageFileServiceIntegrationTest
        {
            public TheGetOrUpdateInfoAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ReadsDataForPackageWithSnupkg()
            {
                // Arrange
                await Target.InitializeAsync();

                var leafItem = new CatalogLeafItem
                {
                    CommitTimestamp = DateTimeOffset.Parse("2021-03-22T20:13:54.6075418Z"),
                    PackageId = "Newtonsoft.Json",
                    PackageVersion = "13.0.1",
                    Type = CatalogLeafType.PackageDetails,
                };

                // Act
                var info = await Target.GetOrUpdateInfoFromLeafItemAsync(leafItem);

                // Assert
                Assert.True(info.Available);
                Assert.Equal("750829", info.HttpHeaders["Content-Length"].Single());
                Assert.Equal("vaWuAPufkOrRT0f48FuX90NnymmB/hrLUvg+XsQUHkaht/cwpqkLYcNlUPrjiak7Uhw4uX14dcXUT/NznHHHHg==", info.HttpHeaders["x-ms-meta-SHA512"].Single());
                Assert.Equal(1, HttpMessageHandlerFactory.Responses.Count(x =>
                    x.RequestMessage.Method == HttpMethod.Get
                    && x.RequestMessage.RequestUri.AbsolutePath.EndsWith(".snupkg")
                    && x.IsSuccessStatusCode));
            }

            [Fact]
            public async Task ReturnsUnavailableForPackageWithNoSnupkg()
            {
                // Arrange
                await Target.InitializeAsync();

                var leafItem = new CatalogLeafItem
                {
                    CommitTimestamp = DateTime.Parse("2018-10-15T01:04:00.4615524Z"),
                    PackageId = "Newtonsoft.Json",
                    PackageVersion = "9.0.1",
                    Type = CatalogLeafType.PackageDetails,
                };

                // Act
                var info = await Target.GetOrUpdateInfoFromLeafItemAsync(leafItem);

                // Assert
                Assert.False(info.Available);
                Assert.Null(info.HttpHeaders);
                Assert.Equal(1, HttpMessageHandlerFactory.Responses.Count(x =>
                    x.RequestMessage.RequestUri.AbsolutePath.EndsWith(".snupkg")
                    && x.StatusCode == HttpStatusCode.NotFound));
                Assert.Equal(0, HttpMessageHandlerFactory.Responses.Count(x =>
                    x.RequestMessage.Method == HttpMethod.Get
                    && x.RequestMessage.RequestUri.AbsolutePath.EndsWith(".snupkg")
                    && x.IsSuccessStatusCode));
            }
        }

        public class TheGetZipDirectoryAsyncMethod : SymbolPackageFileServiceIntegrationTest
        {
            public TheGetZipDirectoryAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task CanReadZipDirectoryFromStorage()
            {
                // Arrange
                await Target.InitializeAsync();

                var leafItem = new CatalogLeafItem
                {
                    CommitTimestamp = DateTimeOffset.Parse("2021-03-22T20:13:54.6075418Z"),
                    PackageId = "Newtonsoft.Json",
                    PackageVersion = "13.0.1",
                    Type = CatalogLeafType.PackageDetails,
                };
                await Target.GetOrUpdateInfoFromLeafItemAsync(leafItem);
                HttpMessageHandlerFactory.Clear();

                // Act
                (var directory, var size, var headers) = await Target.GetZipDirectoryFromLeafItemAsync(leafItem);

                // Assert
                Assert.Equal(11, directory.Entries.Count);
                Assert.Equal(750829, size);
                Assert.Equal("750829", headers["Content-Length"].Single());
                Assert.Equal("vaWuAPufkOrRT0f48FuX90NnymmB/hrLUvg+XsQUHkaht/cwpqkLYcNlUPrjiak7Uhw4uX14dcXUT/NznHHHHg==", headers["x-ms-meta-SHA512"].Single());
            }
        }

        public SymbolPackageFileServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public SymbolPackageFileService Target => Host.Services.GetRequiredService<SymbolPackageFileService>();
    }
}
