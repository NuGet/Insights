using System;
using Knapcode.ExplorePackages.Worker.CatalogLeafItemToCsv;
using Knapcode.ExplorePackages.Worker.FindLatestCatalogLeafScanPerId;
using Knapcode.ExplorePackages.Worker.LoadLatestPackageLeaf;
using Knapcode.ExplorePackages.Worker.LoadPackageArchive;
using Knapcode.ExplorePackages.Worker.LoadPackageManifest;
using Knapcode.ExplorePackages.Worker.LoadPackageVersion;
using Knapcode.ExplorePackages.Worker.NuGetPackageExplorerToCsv;
using Knapcode.ExplorePackages.Worker.PackageArchiveEntryToCsv;
using Knapcode.ExplorePackages.Worker.PackageAssemblyToCsv;
using Knapcode.ExplorePackages.Worker.PackageAssetToCsv;
using Knapcode.ExplorePackages.Worker.PackageManifestToCsv;
using Knapcode.ExplorePackages.Worker.PackageSignatureToCsv;
using Knapcode.ExplorePackages.Worker.PackageVersionToCsv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanDriverFactory : ICatalogScanDriverFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public CatalogScanDriverFactory(IServiceProvider serviceProvider, IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceProvider = serviceProvider;
            _options = options;
        }

        public ICatalogScanDriver Create(CatalogScanDriverType driverType)
        {
            return (ICatalogScanDriver)CreateBatchDriverOrNull(driverType) ?? CreateNonBatchDriver(driverType);
        }

        public ICatalogLeafScanBatchDriver CreateBatchDriverOrNull(CatalogScanDriverType driverType)
        {
            switch (driverType)
            {
                case CatalogScanDriverType.LoadPackageArchive:
                    return _serviceProvider.GetRequiredService<LoadPackageArchiveDriver>();
                case CatalogScanDriverType.LoadPackageManifest:
                    return _serviceProvider.GetRequiredService<LoadPackageManifestDriver>();
                case CatalogScanDriverType.LoadPackageVersion:
                    return _serviceProvider.GetRequiredService<LoadPackageVersionDriver>();
                default:
                    if (_options.Value.RunAllCatalogScanDriversAsBatch)
                    {
                        return WrapNonBatchDriver(CreateNonBatchDriver(driverType));
                    }
                    else
                    {
                        return null;
                    }
            }
        }

        public ICatalogLeafScanNonBatchDriver CreateNonBatchDriver(CatalogScanDriverType driverType)
        {
            switch (driverType)
            {
                case CatalogScanDriverType.Internal_FindLatestCatalogLeafScan:
                    return _serviceProvider.GetRequiredService<FindLatestLeafDriver<CatalogLeafScan>>();
                case CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId:
                    return _serviceProvider.GetRequiredService<FindLatestLeafDriver<CatalogLeafScanPerId>>();

                case CatalogScanDriverType.CatalogLeafItemToCsv:
                    return _serviceProvider.GetRequiredService<CatalogLeafItemToCsvDriver>();
                case CatalogScanDriverType.LoadLatestPackageLeaf:
                    return _serviceProvider.GetRequiredService<FindLatestLeafDriver<LatestPackageLeaf>>();
                case CatalogScanDriverType.PackageArchiveEntryToCsv:
                    return _serviceProvider.GetRequiredService<CatalogLeafScanToCsvAdapter<PackageArchiveEntry>>();
                case CatalogScanDriverType.PackageAssemblyToCsv:
                    return _serviceProvider.GetRequiredService<CatalogLeafScanToCsvAdapter<PackageAssembly>>();
                case CatalogScanDriverType.PackageAssetToCsv:
                    return _serviceProvider.GetRequiredService<CatalogLeafScanToCsvAdapter<PackageAsset>>();
                case CatalogScanDriverType.PackageSignatureToCsv:
                    return _serviceProvider.GetRequiredService<CatalogLeafScanToCsvAdapter<PackageSignature>>();
                case CatalogScanDriverType.PackageManifestToCsv:
                    return _serviceProvider.GetRequiredService<CatalogLeafScanToCsvAdapter<PackageManifestRecord>>();
                case CatalogScanDriverType.PackageVersionToCsv:
                    return _serviceProvider.GetRequiredService<CatalogLeafScanToCsvAdapter<PackageVersionRecord>>();
                case CatalogScanDriverType.NuGetPackageExplorerToCsv:
                    return _serviceProvider.GetRequiredService<CatalogLeafScanToCsvAdapter<NuGetPackageExplorerRecord>>();
                default:
                    throw new NotSupportedException($"Catalog scan driver type '{driverType}' is not supported.");
            }
        }

        private ICatalogLeafScanBatchDriver WrapNonBatchDriver(ICatalogLeafScanNonBatchDriver driver)
        {
            return new CatalogLeafScanBatchDriverAdapter(
                driver,
                _serviceProvider.GetRequiredService<ILogger<CatalogLeafScanBatchDriverAdapter>>());
        }
    }
}
