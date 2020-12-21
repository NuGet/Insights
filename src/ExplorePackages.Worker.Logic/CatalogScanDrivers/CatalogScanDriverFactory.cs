using System;
using Knapcode.ExplorePackages.Worker.FindLatestLeaves;
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

        public ICatalogScanDriver Create(CatalogScanType type)
        {
            switch (type)
            {
                case CatalogScanType.FindLatestLeaves:
                    return _serviceProvider.GetRequiredService<FindLatestLeavesCatalogScanDriver>();
                case CatalogScanType.FindPackageAssets:
                    return _serviceProvider.GetRequiredService<CatalogLeafToCsvAdapter<PackageAsset>>();
                default:
                    throw new NotSupportedException($"Catalog scan type '{type}' is not supported.");
            }
        }
    }
}
