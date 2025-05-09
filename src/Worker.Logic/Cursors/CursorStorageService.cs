// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker
{
    public class CursorStorageService
    {
        private readonly ContainerInitializationState _initializationState;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<CursorStorageService> _logger;

        public CursorStorageService(
            ServiceClientFactory serviceClientFactory,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<CursorStorageService> logger)
        {
            _initializationState = ContainerInitializationState.Table(serviceClientFactory, options.Value, options.Value.CursorTableName);
            _serviceClientFactory = serviceClientFactory;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _initializationState.InitializeAsync();
        }

        public async Task<IReadOnlyList<CursorTableEntity>> GetOrCreateAllAsync(IReadOnlyList<string> names)
        {
            return await GetOrCreateAllAsync(names, CursorTableEntity.Min);
        }

        public async Task<IReadOnlyList<CursorTableEntity>> GetOrCreateAllAsync(IReadOnlyList<string> names, DateTimeOffset defaultValue)
        {
            if (names.Count == 0)
            {
                return Array.Empty<CursorTableEntity>();
            }
            else if (names.Count == 1)
            {
                return new List<CursorTableEntity> { await GetOrCreateAsync(names[0]) };
            }

            var table = await GetTableAsync();
            var min = names.Min(StringComparer.Ordinal);
            var max = names.Max(StringComparer.Ordinal);

            var nameToExisting = await table
                .QueryAsync<CursorTableEntity>(c => c.PartitionKey == string.Empty
                                                 && string.Compare(c.RowKey, min, StringComparison.Ordinal) >= 0
                                                 && string.Compare(c.RowKey, max, StringComparison.Ordinal) <= 0)
                .ToDictionaryAsync(x => x.Name);

            var output = new List<CursorTableEntity>();
            var unique = new HashSet<string>();
            var batch = new MutableTableTransactionalBatch(table);

            foreach (var name in names)
            {
                if (unique.Add(name))
                {
                    if (nameToExisting.TryGetValue(name, out var existing))
                    {
                        output.Add(existing);
                    }
                    else
                    {
                        var cursor = new CursorTableEntity(name) { Value = defaultValue };
                        _logger.LogInformation("Creating cursor {Name} to timestamp {Value:O}.", name, cursor.Value);
                        batch.AddEntity(cursor);
                        output.Add(cursor);
                    }
                }
            }

            await batch.SubmitBatchIfNotEmptyAsync();

            return output;
        }

        public async Task<CursorTableEntity> GetOrCreateAsync(string name, DateTimeOffset defaultValue)
        {
            var table = await GetTableAsync();
            var cursor = await table.GetEntityOrNullAsync<CursorTableEntity>(string.Empty, name);
            if (cursor != null)
            {
                return cursor;
            }
            else
            {
                cursor = new CursorTableEntity(name) { Value = defaultValue };
                _logger.LogInformation("Creating cursor {Name} to timestamp {Value:O}.", name, cursor.Value);
                var response = await table.AddEntityAsync(cursor);
                cursor.UpdateETag(response);
                return cursor;
            }
        }

        public async Task<CursorTableEntity> GetOrCreateAsync(string name)
        {
            return await GetOrCreateAsync(name, CursorTableEntity.Min);
        }

        public async Task UpdateAsync(CursorTableEntity cursor)
        {
            var table = await GetTableAsync();
            _logger.LogInformation("Updating cursor {Name} to timestamp {NewValue:O}.", cursor.Name, cursor.Value);
            var response = await table.UpdateEntityAsync(cursor, cursor.ETag, TableUpdateMode.Replace);
            cursor.UpdateETag(response);
        }

        public async Task UpdateAllAsync(IEnumerable<CursorTableEntity> cursors)
        {
            var table = await GetTableAsync();
            var batch = new MutableTableTransactionalBatch(table);
            foreach (var cursor in cursors)
            {
                _logger.LogInformation("Updating cursor {Name} to timestamp {NewValue:O}.", cursor.Name, cursor.Value);
                batch.UpdateEntity(cursor, cursor.ETag, TableUpdateMode.Replace);
                await batch.SubmitBatchIfFullAsync();
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        private async Task<TableClientWithRetryContext> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync(_options.Value))
                .GetTableClient(_options.Value.CursorTableName);
        }
    }
}
