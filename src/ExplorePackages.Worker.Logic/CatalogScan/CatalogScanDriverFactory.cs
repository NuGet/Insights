using System;
using Knapcode.ExplorePackages.Worker.FindCatalogLeafItems;
using Knapcode.ExplorePackages.Worker.FindLatestPackageLeaves;
using Knapcode.ExplorePackages.Worker.FindPackageAssemblies;
using Knapcode.ExplorePackages.Worker.FindPackageAssets;
using Microsoft.Extensions.DependencyInjection;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanDriverFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public CatalogScanDriverFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ICatalogScanDriver Create(CatalogScanDriverType driverType)
        {
            switch (driverType)
            {
                case CatalogScanDriverType.FindCatalogLeafItems:
                    return _serviceProvider.GetRequiredService<FindCatalogLeafItemsDriver>();
                case CatalogScanDriverType.FindLatestCatalogLeafScans:
                    return _serviceProvider.GetRequiredService<FindLatestLeavesDriver<CatalogLeafScan>>();
                case CatalogScanDriverType.FindLatestPackageLeaves:
                    return _serviceProvider.GetRequiredService<FindLatestLeavesDriver<LatestPackageLeaf>>();
                case CatalogScanDriverType.FindPackageAssemblies:
                    return _serviceProvider.GetRequiredService<CatalogLeafScanToCsvAdapter<PackageAssembly>>();
                case CatalogScanDriverType.FindPackageAssets:
                    return _serviceProvider.GetRequiredService<CatalogLeafScanToCsvAdapter<PackageAsset>>();
                default:
                    throw new NotSupportedException($"Catalog scan driver type '{driverType}' is not supported.");
            }
        }
    }
}
