using System;
using Knapcode.ExplorePackages.Worker.FindCatalogLeafItem;
using Knapcode.ExplorePackages.Worker.FindLatestPackageLeaf;
using Knapcode.ExplorePackages.Worker.FindPackageAssembly;
using Knapcode.ExplorePackages.Worker.FindPackageAsset;
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
                case CatalogScanDriverType.FindCatalogLeafItem:
                    return _serviceProvider.GetRequiredService<FindCatalogLeafItemDriver>();
                case CatalogScanDriverType.FindLatestCatalogLeafScan:
                    return _serviceProvider.GetRequiredService<FindLatestLeafDriver<CatalogLeafScan>>();
                case CatalogScanDriverType.FindLatestPackageLeaf:
                    return _serviceProvider.GetRequiredService<FindLatestLeafDriver<LatestPackageLeaf>>();
                case CatalogScanDriverType.FindPackageAssembly:
                    return _serviceProvider.GetRequiredService<CatalogLeafScanToCsvAdapter<PackageAssembly>>();
                case CatalogScanDriverType.FindPackageAsset:
                    return _serviceProvider.GetRequiredService<CatalogLeafScanToCsvAdapter<PackageAsset>>();
                default:
                    throw new NotSupportedException($"Catalog scan driver type '{driverType}' is not supported.");
            }
        }
    }
}
