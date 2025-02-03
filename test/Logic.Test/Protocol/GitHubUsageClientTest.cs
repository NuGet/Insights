// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class GitHubUsageClientTest : BaseLogicIntegrationTest
    {
        private const string GitHubUsageToCsvDir = "GitHubUsageToCsv";

        public GitHubUsageClientTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task FetchesGitHubUsage()
        {
            // Arrange
            var url = $"http://localhost/{TestInput}/{GitHubUsageToCsvDir}/{Step1}/GitHubUsage.v1.json";
            ConfigureSettings = x => x.GitHubUsageV1Urls = new List<string> { url };
            HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/GitHubUsage.v1.json", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri(url);
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };

            // Set the Last-Modified date
            var asOfTimestamp = DateTimeOffset.Parse("2021-01-16T20:00:00Z", CultureInfo.InvariantCulture);
            var client = Host.Services.GetRequiredService<GitHubUsageClient>();

            // Act
            await using var set = await client.GetAsync();

            // Assert
            Assert.Equal(asOfTimestamp, set.AsOfTimestamp);
            Assert.Equal(url, set.Url.AbsoluteUri);
            Assert.Equal("\"1d6ec422bbfe2d7\"", set.ETag);
            var repoInfo = await set.Entries.ToListAsync();

            Assert.Equal(2, repoInfo.Count);
            Assert.Equal("xunit/xunit", repoInfo[0].Id);
            Assert.Equal(4273, repoInfo[0].Stars);
            Assert.Equal(["Nerdbank.GitVersioning", "Microsoft.NET.Test.Sdk", "Moq"], repoInfo[0].Dependencies);
            Assert.Equal("NuGet/NuGetGallery", repoInfo[1].Id);
            Assert.Equal(1561, repoInfo[1].Stars);
            Assert.Equal(["Knapcode.MiniZip", "CsvHelper", "Moq", "WindowsAzure.Storage", "NuGet.Versioning"], repoInfo[1].Dependencies);
        }
    }
}
