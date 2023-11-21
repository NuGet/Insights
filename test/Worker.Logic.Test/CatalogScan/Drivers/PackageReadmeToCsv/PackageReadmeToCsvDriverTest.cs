// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Net;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using System.Net.Http.Headers;

namespace NuGet.Insights.Worker.PackageReadmeToCsv
{
    public class PackageReadmeToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageReadmeToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public ICatalogLeafToCsvDriver<PackageReadme> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageReadme>>();

        [Fact]
        public async Task HandlesNoReadme()
        {
            // Arrange
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                PackageId = "Newtonsoft.Json",
                PackageVersion = "9.0.1",
                LeafType = CatalogLeafType.PackageDetails,
                Url = "https://api.nuget.org/v3/catalog0/data/2022.12.08.16.43.03/newtonsoft.json.9.0.1.json",
            };

            // Act
            var output = await Target.ProcessLeafAsync(leaf);

            // Assert
            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageReadmeResultType.None, record.ResultType);
            Assert.Null(record.Content);
        }

        [Fact]
        public async Task HandlesLegacyReadme()
        {
            // Arrange
            var content = "my README that is not embedded";
            ConfigureSettings = x => x.LegacyReadmeUrlPattern = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/legacy-readme";
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                if (r.RequestUri.LocalPath.EndsWith("/legacy-readme", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        RequestMessage = r,
                        Content = new StringContent(content)
                        {
                            Headers =
                            {
                                ContentType = new MediaTypeHeaderValue("text/plain"),
                            },
                        },
                    };
                }

                return await b(r, t);
            };

            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                PackageId = "McMaster.Extensions.CommandLineUtils",
                PackageVersion = "2.2.2",
                LeafType = CatalogLeafType.PackageDetails,
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.07.10.51.01/mcmaster.extensions.commandlineutils.2.2.2.json",
            };

            // Act
            var output = await Target.ProcessLeafAsync(leaf);

            // Assert
            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageReadmeResultType.Legacy, record.ResultType);
            Assert.Equal(content, record.Content);
        }

        [Fact]
        public async Task HandlesEmbeddedReadme()
        {
            // Arrange
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                PackageId = "Knapcode.TorSharp",
                PackageVersion = "2.8.1",
                LeafType = CatalogLeafType.PackageDetails,
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.06.00.31.41/knapcode.torsharp.2.8.1.json",
            };

            // Act
            var output = await Target.ProcessLeafAsync(leaf);

            // Assert
            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageReadmeResultType.Embedded, record.ResultType);
            Assert.StartsWith("# TorSharp", record.Content, StringComparison.Ordinal);
        }
    }
}
