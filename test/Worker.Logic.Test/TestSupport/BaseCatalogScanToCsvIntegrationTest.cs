using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public abstract class BaseCatalogScanToCsvIntegrationTest<T> : BaseCatalogScanIntegrationTest where T : ICsvRecord
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

    public abstract class BaseCatalogScanToCsvIntegrationTest<T1, T2> : BaseCatalogScanIntegrationTest
        where T1 : ICsvRecord
        where T2 : ICsvRecord
    {
        public BaseCatalogScanToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected abstract string DestinationContainerName1 { get; }
        protected abstract string DestinationContainerName2 { get; }

        protected override IEnumerable<string> GetExpectedBlobContainerNames()
        {
            yield return DestinationContainerName1;
            yield return DestinationContainerName2;
        }

        protected async Task AssertOutputAsync(string testName, string stepName, int bucket)
        {
            await AssertCompactAsync<T1>(DestinationContainerName1, testName, Path.Combine(stepName, "T1"), bucket);
            await AssertCompactAsync<T2>(DestinationContainerName2, testName, Path.Combine(stepName, "T2"), bucket);
        }
    }
}
