// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.LoadPackageVersion
{
    public class PackageVersionStorage : ILatestPackageLeafStorage<PackageVersionEntity>
    {
        private readonly CatalogClient _catalogClient;

        public PackageVersionStorage(
            TableClientWithRetryContext tableClient,
            CatalogClient catalogClient)
        {
            Table = tableClient;
            _catalogClient = catalogClient;
        }

        public TableClientWithRetryContext Table { get; }
        public string CommitTimestampColumnName => nameof(PackageVersionEntity.CommitTimestamp);

        public (string PartitionKey, string RowKey) GetKey(ICatalogLeafItem item)
        {
            return (PackageVersionEntity.GetPartitionKey(item.PackageId), PackageVersionEntity.GetRowKey(item.PackageVersion));
        }

        public async Task<PackageVersionEntity> MapAsync(string partitionKey, string rowKey, ICatalogLeafItem item)
        {
            if (item.LeafType == CatalogLeafType.PackageDelete)
            {
                return new PackageVersionEntity(partitionKey, rowKey, item);
            }

            var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item);

            return new PackageVersionEntity(partitionKey, rowKey, item, leaf);
        }
    }
}
