// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.FindLatestCatalogLeafScanPerId;

#nullable enable

namespace NuGet.Insights.Worker
{
    public class CatalogScanDriverFactory : ICatalogScanDriverFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public CatalogScanDriverFactory(IServiceProvider serviceProvider, IOptions<NuGetInsightsWorkerSettings> options)
        {
            _serviceProvider = serviceProvider;
            _options = options;
        }

        public ICatalogScanDriver Create(CatalogScanDriverType driverType)
        {
            return (ICatalogScanDriver?)CreateBatchDriverOrNull(driverType) ?? CreateNonBatchDriver(driverType);
        }

        public ICatalogLeafScanBatchDriver? CreateBatchDriverOrNull(CatalogScanDriverType driverType)
        {
            if (driverType == CatalogScanDriverType.Internal_FindLatestCatalogLeafScan)
            {
                return _serviceProvider.GetRequiredService<FindLatestLeafDriver<CatalogLeafScan>>();
            }

            if (driverType == CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId)
            {
                return _serviceProvider.GetRequiredService<FindLatestLeafDriver<CatalogLeafScanPerId>>();
            }

            if (CatalogScanDriverMetadata.IsBatchDriver(driverType))
            {
                return (ICatalogLeafScanBatchDriver)_serviceProvider.GetRequiredService(CatalogScanDriverMetadata.GetRuntimeType(driverType));
            }

            return null;
        }

        public ICatalogLeafScanNonBatchDriver CreateNonBatchDriver(CatalogScanDriverType driverType)
        {
            var driver = CreateNonBatchDriverOrNull(driverType);
            if (driver is null)
            {
                throw new NotSupportedException($"Catalog scan driver type '{driverType}' is not supported.");
            }

            return driver;
        }

        public ICatalogLeafScanNonBatchDriver? CreateNonBatchDriverOrNull(CatalogScanDriverType driverType)
        {
            if (!CatalogScanDriverMetadata.IsBatchDriver(driverType))
            {
                return (ICatalogLeafScanNonBatchDriver)_serviceProvider.GetRequiredService(CatalogScanDriverMetadata.GetRuntimeType(driverType));
            }

            return null;
        }

        private ICatalogLeafScanBatchDriver WrapNonBatchDriver(ICatalogLeafScanNonBatchDriver driver)
        {
            return new CatalogLeafScanBatchDriverAdapter(
                driver,
                _serviceProvider.GetRequiredService<ILogger<CatalogLeafScanBatchDriverAdapter>>());
        }
    }
}
