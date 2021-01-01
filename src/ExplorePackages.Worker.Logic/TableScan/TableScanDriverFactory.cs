using System;
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

        public ITableScanDriver<T> Create(TableScanType type)
        {
            switch (type)
            {
                case TableScanType.TableCopy:
                    return _serviceProvider.GetRequiredService<TableCopyDriver<T>>();
                default:
                    throw new NotSupportedException($"Table scan type '{type}' is not supported.");
            }
        }
    }
}
