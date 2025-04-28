// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageDownloadsClientIntegrationTest : BaseLogicIntegrationTest
    {
        [RealStorageTokenCredentialFact]
        public async Task CanFetchDataFromPrivateAzureStorage()
        {
            // Arrange
            var serviceClientFactory = new ServiceClientFactory(
                Microsoft.Extensions.Options.Options.Create(new NuGetInsightsSettings().WithTestStorageSettings()),
                TelemetryClient,
                Output.GetLoggerFactory());
            var serviceClient = await serviceClientFactory.GetBlobServiceClientAsync(Options.Value);
            var container = serviceClient.GetBlobContainerClient($"{StoragePrefix}1b1");
            await container.CreateIfNotExistsAsync();
            var blob = container.GetBlobClient("downloads.v1.json");
            await blob.UploadAsync(Resources.LoadMemoryStream(TestDataPath));

            Options.Value.UseBlobClientForExternalData = true;
            Options.Value.DownloadsV1Urls = new List<string> { blob.Uri.AbsoluteUri };
            Options.Value.DownloadsV1AgeLimit = TimeSpan.MaxValue;

            var expected = await PackageDownloadsClient.DeserializeV1Async(Resources.LoadMemoryStream(TestDataPath)).ToListAsync();

            // Act
            await using var data = await Target.GetAsync();

            // Assert
            var actual = await data.Pages.ToListAsync();
            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual);
        }

        public IPackageDownloadsClient Target => Host.Services.GetRequiredService<IPackageDownloadsClient>();
        private string TestDataPath => Path.Combine("DownloadsToCsv", Step1, "downloads.v1.json");

        public PackageDownloadsClientIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
