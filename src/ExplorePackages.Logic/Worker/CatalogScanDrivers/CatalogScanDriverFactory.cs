using System;
using Microsoft.Extensions.DependencyInjection;

namespace Knapcode.ExplorePackages.Logic.Worker
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
                case CatalogScanType.DownloadPages:
                    return _serviceProvider.GetRequiredService<DownloadPagesCatalogScanDriver>();
                case CatalogScanType.DownloadLeaves:
                    return _serviceProvider.GetRequiredService<DownloadLeavesCatalogScanDriver>();
                case CatalogScanType.FindLatestLeaves:
                    return _serviceProvider.GetRequiredService<FindLatestLeavesCatalogScanDriver>();
                default:
                    throw new NotSupportedException($"Catalog scan type '{type}' is not supported.");
            }
        }
    }
}
