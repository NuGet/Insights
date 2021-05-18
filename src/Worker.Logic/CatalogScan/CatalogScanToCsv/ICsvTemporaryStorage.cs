// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface ICsvTemporaryStorage
    {
        Task AppendAsync<T>(string storageSuffix, ICsvRecordSet<T> set) where T : class, ICsvRecord;
        Task FinalizeAsync(CatalogIndexScan indexScan);
        Task InitializeAsync(CatalogIndexScan indexScan);
        Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan);
        Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan);
        Task StartAggregateAsync(CatalogIndexScan indexScan);
        Task StartCustomExpandAsync(CatalogIndexScan indexScan);
    }
}
