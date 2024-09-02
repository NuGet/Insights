// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker
{
    public interface ILatestPackageLeafStorage<T> where T : ILatestPackageLeaf
    {
        TableClientWithRetryContext Table { get; }
        string CommitTimestampColumnName { get; }
        LatestLeafStorageStrategy Strategy { get; }
        Task<T> MapAsync(string partitionKey, string rowKey, ICatalogLeafItem item);
        (string PartitionKey, string RowKey) GetKey(ICatalogLeafItem item);
    }
}
