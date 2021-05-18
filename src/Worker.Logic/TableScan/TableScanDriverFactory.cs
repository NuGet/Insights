// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Insights.Worker.EnqueueCatalogLeafScan;
using NuGet.Insights.Worker.TableCopy;

namespace NuGet.Insights.Worker
{
    public class TableScanDriverFactory<T> where T : class, ITableEntity, new()
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
