// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageReadmeIntegrationTest : BaseLogicIntegrationTest
    {
        [Fact]
        public async Task ReturnsEmbeddedReadmeContent()
        {
            // Arrange
            await Target.InitializeAsync();
            var leaf = new PackageIdentityCommit
            {
                PackageId = "Knapcode.TorSharp",
                PackageVersion = "2.8.1",
                LeafType = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2021-08-06T00:31:15.51Z", CultureInfo.InvariantCulture),
            };

            // Act
            var info = await Target.GetOrUpdateInfoFromLeafItemAsync(leaf);

            // Assert
            Assert.Single(HttpMessageHandlerFactory
                .SuccessRequests
                .Where(x => x.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal)));
            Assert.Equal(ReadmeType.Embedded, info.ReadmeType);
            Assert.Equal(7628, info.ReadmeBytes.Length);
            Assert.NotEmpty(info.HttpHeaders);
            var readme = Encoding.UTF8.GetString(info.ReadmeBytes.Span);
            Assert.StartsWith("# TorSharp", readme, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReturnsNoneForNoReadmeContent()
        {
            // Arrange
            await Target.InitializeAsync();
            var leaf = new PackageIdentityCommit
            {
                PackageId = "Knapcode.TorSharp",
                PackageVersion = "1.0.0",
                LeafType = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2021-08-06T00:31:41.2929519Z", CultureInfo.InvariantCulture),
            };

            // Act
            var info = await Target.GetOrUpdateInfoFromLeafItemAsync(leaf);

            // Assert
            Assert.Single(HttpMessageHandlerFactory
                .Responses
                .Where(x => x.StatusCode == HttpStatusCode.NotFound)
                .Where(x => x.RequestMessage.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal)));
            Assert.Equal(ReadmeType.None, info.ReadmeType);
        }

        [Fact]
        public async Task ReturnsReadmeContentWithLegacyPattern()
        {
            // This URL pattern is fake.
            ConfigureSettings = x => x.LegacyReadmeUrlPattern = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/legacy-readme";
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                if (r.RequestUri.LocalPath.EndsWith("/legacy-readme", StringComparison.Ordinal))
                {
                    var stream = Resources.LoadMemoryStream(Resources.READMEs.WindowsAzure_Storage_9_3_3);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        RequestMessage = r,
                        Content = new StreamContent(stream)
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

            // Arrange
            await Target.InitializeAsync();
            var leaf = new PackageIdentityCommit
            {
                PackageId = "WindowsAzure.Storage",
                PackageVersion = "9.3.3",
                LeafType = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2020-07-08T17:12:18.5692562Z", CultureInfo.InvariantCulture),
            };

            // Act
            var info = await Target.GetOrUpdateInfoFromLeafItemAsync(leaf);

            // Assert
            Assert.Single(HttpMessageHandlerFactory
                .Responses
                .Where(x => x.StatusCode == HttpStatusCode.NotFound)
                .Where(x => x.RequestMessage.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal)));
            Assert.Single(HttpMessageHandlerFactory
                .SuccessRequests
                .Where(x => x.RequestUri.AbsolutePath.EndsWith("/legacy-readme", StringComparison.Ordinal)));
            Assert.Equal(ReadmeType.Legacy, info.ReadmeType);
            Assert.Equal(618, info.ReadmeBytes.Length);
            Assert.NotEmpty(info.HttpHeaders);
            var readme = Encoding.UTF8.GetString(info.ReadmeBytes.Span);
            Assert.StartsWith("Development on this library has shifted focus to the Azure Unified SDK.", readme, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReturnsNoneForDeletedPackage()
        {
            // Arrange
            await Target.InitializeAsync();
            var leaf = new PackageIdentityCommit
            {
                PackageId = "nuget.platform",
                PackageVersion = "1.0.0",
                LeafType = CatalogLeafType.PackageDelete,
                CommitTimestamp = DateTimeOffset.Parse("2017-11-08T17:42:28.5677911", CultureInfo.InvariantCulture),
            };

            // Act
            var info = await Target.GetOrUpdateInfoFromLeafItemAsync(leaf);

            // Assert
            Assert.Empty(HttpMessageHandlerFactory.Responses.Where(x => x.RequestMessage.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal)));
            Assert.Empty(HttpMessageHandlerFactory.Responses.Where(x => x.RequestMessage.RequestUri.AbsolutePath.EndsWith("/legacy-readme", StringComparison.Ordinal)));
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
