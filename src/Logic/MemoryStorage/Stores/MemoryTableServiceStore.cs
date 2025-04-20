// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Azure.Data.Tables.Models;

namespace NuGet.Insights.MemoryStorage
{
    public class MemoryTableServiceStore
    {
        public static MemoryTableServiceStore SharedStore { get; } = new(TimeProvider.System);

        private readonly ConcurrentDictionary<string, MemoryTableStore> _tables = new();
        private readonly TimeProvider _timeProvider;

        public MemoryTableServiceStore(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public virtual IEnumerable<TableItem> GetTableItems(Func<TableItem, bool> filter)
        {
            return _tables
                .Select(x => x.Value.GetTableItem())
                .Where(x => x.Type switch
                {
                    StorageResultType.Success => true,
                    StorageResultType.DoesNotExist => false,
                    _ => throw new NotImplementedException("Unexpected result type: " + x.Type),
                })
                .Select(x => x.Value)
                .Where(filter)
                .OrderBy(x => x.Name, StringComparer.Ordinal);
        }

        public virtual MemoryTableStore GetTable(string name)
        {
            return _tables.GetOrAdd(name, x => new MemoryTableStore(_timeProvider, x));
        }

        public virtual StorageResultType DeleteTable(string tableName)
        {
            if (!_tables.TryGetValue(tableName, out var table))
            {
                return StorageResultType.DoesNotExist;
            }

            return table.Delete();
        }
    }
}
