// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageDownloadsClientIntegrationTest : BaseLogicIntegrationTest
    {
        [StorageTokenCredentialFact]
        public async Task CanFetchDataFromPrivateAzureStorage()
        {
            // Arrange
            var serviceClientFactory = new ServiceClientFactory(
                Options.Create(new NuGetInsightsSettings().WithTestStorageSettings()),
                Output.GetLoggerFactory());
            var blobClient = await serviceClientFactory.GetBlobServiceClientAsync();
            var container = blobClient.GetBlobContainerClient($"{StoragePrefix}1b1");
            await container.CreateIfNotExistsAsync();
            var blob = container.GetBlobClient("downloads.v1.json");
            await blob.UploadAsync(Resources.LoadMemoryStream(TestDataPath));

            ConfigureSettings = x =>
            {
                x.UseBlobClientForExternalData = true;
                x.DownloadsV1Urls = [blob.Uri.AbsoluteUri];
                x.DownloadsV1AgeLimit = TimeSpan.MaxValue;
            };

            var expected = await PackageDownloadsClient.DeserializeV1Async(Resources.LoadMemoryStream(TestDataPath)).ToListAsync();

            // Act
            await using var data = await Target.GetAsync();

            // Assert
            var actual = await data.Entries.ToListAsync();
            Assert.NotEmpty(actual);
            Assert.Equal(expected, actual);
        }

        public PackageDownloadsClient Target => Host.Services.GetRequiredService<PackageDownloadsClient>();
        private string TestDataPath => Path.Combine("DownloadsToCsv", Step1, "downloads.v1.json");

        public PackageDownloadsClientIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
