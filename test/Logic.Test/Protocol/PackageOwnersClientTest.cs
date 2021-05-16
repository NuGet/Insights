using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class PackageOwnersClientTest : BaseLogicIntegrationTest
    {
        private const string OwnersToCsvDir = "OwnersToCsv";

        public PackageOwnersClientTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task ExecuteAsync()
        {
            // Arrange
            var ownersV2Url = $"http://localhost/{TestData}/{OwnersToCsvDir}/{Step1}/owners.v2.json";
            ConfigureSettings = x => x.OwnersV2Url = ownersV2Url;
            HttpMessageHandlerFactory.OnSendAsync = async req =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json"))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri(ownersV2Url);
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };

            // Set the Last-Modified date
            var asOfTimestamp = DateTime.Parse("2021-01-14T18:00:00Z");
            var downloadsFile = new FileInfo(Path.Combine(TestData, OwnersToCsvDir, Step1, "owners.v2.json"))
            {
                LastWriteTimeUtc = asOfTimestamp,
            };

            var client = Host.Services.GetRequiredService<PackageOwnersClient>();

            // Act
            await using var set = await client.GetPackageOwnerSetAsync();

            // Assert
            Assert.Equal(asOfTimestamp, set.AsOfTimestamp);
            Assert.Equal(ownersV2Url, set.Url);
            Assert.Equal("\"1d6ea9f136390e4\"", set.ETag);
            var owners = new List<PackageOwner>();
            await foreach (var owner in set.Owners)
            {
                owners.Add(owner);
            }

            Assert.Equal(
                new[]
                {
                    new PackageOwner("Microsoft.Extensions.Logging", "Microsoft"),
                    new PackageOwner("Microsoft.Extensions.Logging", "aspnet"),
                    new PackageOwner("Microsoft.Extensions.Logging", "dotnetframework"),
                    new PackageOwner("Knapcode.TorSharp", "joelverhagen"),
                    new PackageOwner("Castle.Core", "castleproject"),
                },
                owners.ToArray());
        }
    }
}
