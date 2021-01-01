using System;
using Knapcode.ExplorePackages.Worker.FindLatestLeaves;
using Knapcode.ExplorePackages.Worker.LatestLeafToLeafScan;
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
                case TableScanDriverType.LatestLeafToLeafScan when typeof(T) == typeof(LatestPackageLeaf):
                    var driver = _serviceProvider.GetRequiredService<LatestLeafToLeafScanDriver>();
                    return (ITableScanDriver<T>)driver;
                default:
                    throw new NotSupportedException($"Table scan driver type '{driverType}' and entity type '{typeof(T)}' is not supported.");
            }
        }
    }
}
