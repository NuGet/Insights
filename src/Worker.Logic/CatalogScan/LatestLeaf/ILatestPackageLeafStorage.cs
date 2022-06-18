// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker
{
    public interface ILatestPackageLeafStorage<T> where T : ILatestPackageLeaf
    {
        TableClient Table { get; }
        Task<T> MapAsync(ICatalogLeafItem item);
        string GetPartitionKey(ICatalogLeafItem item);
        string GetRowKey(ICatalogLeafItem item);
        string CommitTimestampColumnName { get; }
    }
}
