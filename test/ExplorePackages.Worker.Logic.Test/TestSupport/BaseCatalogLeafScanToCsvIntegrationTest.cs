using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public abstract class BaseCatalogLeafScanToCsvIntegrationTest : BaseCatalogScanToCsvIntegrationTest
    {
        protected BaseCatalogLeafScanToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public virtual bool OnlyLatestLeaves => true;

        protected override Task<CatalogIndexScan> UpdateAsync(DateTimeOffset max)
        {
            return UpdateAsync(DriverType, OnlyLatestLeaves, max);
        }

        protected override IEnumerable<string> GetExpectedLeaseNames()
        {
            if (OnlyLatestLeaves)
            {
                yield return $"Start-CatalogScan-{CatalogScanDriverType.FindLatestCatalogLeafScan}";
            }
        }
    }
}
