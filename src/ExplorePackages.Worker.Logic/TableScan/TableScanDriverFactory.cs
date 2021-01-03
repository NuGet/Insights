using System;
using Knapcode.ExplorePackages.Worker.EnqueueCatalogLeafScan;
using Knapcode.ExplorePackages.Worker.FindLatestPackageLeaves;
using Knapcode.ExplorePackages.Worker.TableCopy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public class TableScanDriverFactory<T> where T : ITableEntity, new()
    {
        private readonly IServiceProvider _serviceProvider;

        public TableScanDriverFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ITableScanDriver<T> Create(TableScanDriverType driverType)
        {
            switch (driverType)
            {
                case TableScanDriverType.TableCopy:
                    return _serviceProvider.GetRequiredService<TableCopyDriver<T>>();
                case TableScanDriverType.EnqueueCatalogLeafScans when typeof(T) == typeof(CatalogLeafScan):
                    return (ITableScanDriver<T>)_serviceProvider.GetRequiredService<EnqueueCatalogLeafScansDriver>();
                default:
                    throw new NotSupportedException($"Table scan driver type '{driverType}' and entity type '{typeof(T)}' is not supported.");
            }
        }
    }
}
