using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.FindLatestLeaves
{
    public class FindLatestLeavesIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string FindLatestLeavesDir = nameof(FindLatestLeaves);

        public FindLatestLeavesIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.FindLatestLeaves;

        public class FindLatestLeaves : FindLatestLeavesIntegrationTest
        {
            public FindLatestLeaves(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            protected override IEnumerable<string> GetExpectedTableNames()
            {
                yield return Options.Value.LatestLeavesTableName;
            }

            [Fact]
            public async Task Execute()
            {
                Logger.LogInformation("Settings: " + Environment.NewLine + JsonConvert.SerializeObject(Options.Value, Formatting.Indented));

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z");
                var max1 = DateTimeOffset.Parse("2020-12-27T05:07:21.9968244Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                var leaves = await ServiceClientFactory
                    .GetStorageAccount()
                    .CreateCloudTableClient()
                    .GetTableReference(Options.Value.LatestLeavesTableName)
                    .GetEntitiesAsync<LatestPackageLeaf>(TelemetryClient.NewQueryLoopMetrics());

                foreach (var leaf in leaves)
                {
                    leaf.ETag = null;
                    leaf.Timestamp = DateTimeOffset.MinValue;
                }

                var serializerSettings = NameVersionSerializer.JsonSerializerSettings;
                serializerSettings.NullValueHandling = NullValueHandling.Include;
                serializerSettings.Formatting = Formatting.Indented;
                var actual = JsonConvert.SerializeObject(leaves, serializerSettings);
                // Directory.CreateDirectory(Path.Combine(TestData, FindLatestLeavesDir));
                // File.WriteAllText(Path.Combine(TestData, FindLatestLeavesDir, "leaves.json"), actual);
                var expected = File.ReadAllText(Path.Combine(TestData, FindLatestLeavesDir, "leaves.json"));
                Assert.Equal(expected, actual);

                await VerifyExpectedContainersAsync();
            }
        }
    }
}
