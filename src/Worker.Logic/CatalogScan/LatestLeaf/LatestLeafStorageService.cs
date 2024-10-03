// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;
using static NuGet.Insights.StorageUtility;

namespace NuGet.Insights.Worker
{
    public class LatestLeafStorageService<T> where T : class, ILatestPackageLeaf, new()
    {
        private readonly EntityUpsertStorageService<ICatalogLeafItem, T> _storageService;

        public LatestLeafStorageService(EntityUpsertStorageService<ICatalogLeafItem, T> storageService)
        {
            _storageService = storageService;
        }

        public async Task AddAsync(IReadOnlyList<ICatalogLeafItem> items, ILatestPackageLeafStorage<T> storage)
        {
            await _storageService.AddAsync(items, new Adapter(storage));
        }

        private class Adapter : IEntityUpsertStorage<ICatalogLeafItem, T>
        {
            private readonly ILatestPackageLeafStorage<T> _storage;

            public Adapter(ILatestPackageLeafStorage<T> storage)
            {
                _storage = storage;
                Select = [RowKey, _storage.CommitTimestampColumnName];
            }

            public IReadOnlyList<string> Select { get; }
            public EntityUpsertStrategy Strategy => _storage.Strategy;
            public TableClientWithRetryContext Table => _storage.Table;

            public ItemWithEntityKey<ICatalogLeafItem> GetItemFromRowKeyGroup(IGrouping<string, ItemWithEntityKey<ICatalogLeafItem>> group)
            {
                return group.OrderByDescending(x => x.Item.CommitTimestamp).First();
            }

            public (string PartitionKey, string RowKey) GetKey(ICatalogLeafItem item)
            {
                return _storage.GetKey(item);
            }

            public Task<T> MapAsync(string partitionKey, string rowKey, ICatalogLeafItem item)
            {
                return _storage.MapAsync(partitionKey, rowKey, item);
            }

            public bool ShouldReplace(ICatalogLeafItem item, T entity)
            {
                return item.CommitTimestamp > entity.CommitTimestamp;
            }
        }
    }
}
