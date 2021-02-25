using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public abstract class BaseCatalogScanToCsvIntegrationTest<T> : BaseCatalogScanIntegrationTest where T : ICsvRecord<T>, new()
    {
        public BaseCatalogScanToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected abstract string DestinationContainerName { get; }

        protected override IEnumerable<string> GetExpectedBlobContainerNames()
        {
            yield return DestinationContainerName;
        }

        protected async Task AssertOutputAsync(string testName, string stepName, int bucket)
        {
            await AssertCompactAsync<T>(DestinationContainerName, testName, stepName, bucket);
        }
    }
}
