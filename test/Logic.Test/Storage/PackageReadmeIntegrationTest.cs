// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageReadmeIntegrationTest : BaseLogicIntegrationTest
    {
        public const string WindowsAzure_Storage_9_3_3 = "WindowsAzure.Storage.9.3.3.md";

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
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests, x => x.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal));
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
            Assert.Single(HttpMessageHandlerFactory.Responses.Where(x => x.StatusCode == HttpStatusCode.NotFound), x => x.RequestMessage.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal));
            Assert.Equal(ReadmeType.None, info.ReadmeType);
        }

        [RealStorageTokenCredentialFact]
        public async Task ReturnsReadmeContentWithLegacyPatternFromPrivateAzureStorage()
        {
            // Arrange
            var serviceClientFactory = new ServiceClientFactory(
                Microsoft.Extensions.Options.Options.Create(new NuGetInsightsSettings().WithTestStorageSettings()),
                TelemetryClient,
                Output.GetLoggerFactory());
            var blobClient = await serviceClientFactory.GetBlobServiceClientAsync();
            var container = blobClient.GetBlobContainerClient($"{StoragePrefix}1lr1");
            await container.CreateIfNotExistsAsync();
            var blob = container.GetBlobClient("windowsazure.storage/9.3.3/legacy-readme");
            await blob.UploadAsync(Resources.LoadMemoryStream(WindowsAzure_Storage_9_3_3));

            ConfigureSettings = x =>
            {
                x.UseBlobClientForExternalData = true;
                x.LegacyReadmeUrlPattern = container.Uri.AbsoluteUri + "/{0}/{1}/legacy-readme";
            };

            var expected = await Resources.LoadStringReader(WindowsAzure_Storage_9_3_3).ReadToEndAsync();

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
            Assert.Equal(ReadmeType.Legacy, info.ReadmeType);
            Assert.Equal(618, info.ReadmeBytes.Length);
            Assert.NotEmpty(info.HttpHeaders);
            var readme = Encoding.UTF8.GetString(info.ReadmeBytes.Span);
            Assert.Equal(expected, readme);
        }

        [Fact]
        public async Task ReturnsReadmeContentWithLegacyPattern()
        {
            // This URL pattern is fake.
            ConfigureSettings = x =>
            {
                x.UseBlobClientForExternalData = false;
                x.LegacyReadmeUrlPattern = "https://api.nuget.org/v3-flatcontainer/{0}/{1}/legacy-readme";
            };
            HttpMessageHandlerFactory.OnSendAsync = async (r, b, t) =>
            {
                if (r.RequestUri.LocalPath.EndsWith("/legacy-readme", StringComparison.Ordinal))
                {
                    var stream = Resources.LoadMemoryStream(WindowsAzure_Storage_9_3_3);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        RequestMessage = r,
                        Headers =
                        {
                            ETag = new EntityTagHeaderValue("\"some-etag\""),
                        },
                        Content = new StreamContent(stream)
                        {
                            Headers =
                            {
                                ContentType = new MediaTypeHeaderValue("text/plain"),
                                LastModified = DateTimeOffset.Parse("2024-08-11T08:15:00Z", CultureInfo.InvariantCulture),
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
            Assert.Single(HttpMessageHandlerFactory.Responses.Where(x => x.StatusCode == HttpStatusCode.NotFound), x => x.RequestMessage.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal));
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests, x => x.RequestUri.AbsolutePath.EndsWith("/legacy-readme", StringComparison.Ordinal));
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
            Assert.DoesNotContain(HttpMessageHandlerFactory.Responses, x => x.RequestMessage.RequestUri.AbsolutePath.EndsWith("/readme", StringComparison.Ordinal));
            Assert.DoesNotContain(HttpMessageHandlerFactory.Responses, x => x.RequestMessage.RequestUri.AbsolutePath.EndsWith("/legacy-readme", StringComparison.Ordinal));
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
