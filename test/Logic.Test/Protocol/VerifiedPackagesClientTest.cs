// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class VerifiedPackagesClientTest : BaseLogicIntegrationTest
    {
        private const string VerifiedPackagesToCsvDir = "VerifiedPackagesToCsv";

        public VerifiedPackagesClientTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task FetchesVerifiedPackages()
        {
            // Arrange
            var url = $"http://localhost/{TestInput}/{VerifiedPackagesToCsvDir}/{Step1}/verifiedPackages.json";
            ConfigureSettings = x => x.VerifiedPackagesV1Urls = new List<string> { url };
            HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/verifiedPackages.json", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri(url);
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };

            // Set the Last-Modified date
            var asOfTimestamp = DateTimeOffset.Parse("2021-01-14T18:00:00Z", CultureInfo.InvariantCulture);
            var client = Host.Services.GetRequiredService<VerifiedPackagesClient>();

            // Act
            await using var set = await client.GetAsync();

            // Assert
            Assert.Equal(asOfTimestamp, set.AsOfTimestamp);
            Assert.Equal(url, set.Url.AbsoluteUri);
            Assert.Equal("\"1d6ea9f13639062\"", set.ETag);
            var verifiedPackages = new List<VerifiedPackage>();
            await foreach (var page in set.Pages)
            {
                verifiedPackages.AddRange(page);
            }

            Assert.Equal(
                new[]
                {
                    new VerifiedPackage("Microsoft.Extensions.Logging"),
                    new VerifiedPackage("Knapcode.TorSharp"),
                    new VerifiedPackage("Castle.Core"),
                    new VerifiedPackage("Newtonsoft.Json"),
                },
                verifiedPackages.ToArray());
        }
    }
}
