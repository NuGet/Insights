// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface ICatalogScanDriver
    {
        Task InitializeAsync(CatalogIndexScan indexScan);
        Task<CatalogIndexScanResult> ProcessIndexAsync(CatalogIndexScan indexScan);
        Task StartCustomExpandAsync(CatalogIndexScan indexScan);
        Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan);
        Task<CatalogPageScanResult> ProcessPageAsync(CatalogPageScan pageScan);
        Task StartAggregateAsync(CatalogIndexScan indexScan);
        Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan);
        Task FinalizeAsync(CatalogIndexScan indexScan);
    }
}
