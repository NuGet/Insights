// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker
{
    public class CursorStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<CursorStorageService> _logger;

        public CursorStorageService(
            ServiceClientFactory serviceClientFactory,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<CursorStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await (await GetTableAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task<IReadOnlyList<CursorTableEntity>> GetOrCreateAllAsync(IReadOnlyList<string> names)
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
                .QueryAsync<CursorTableEntity>(c => c.PartitionKey == string.Empty && c.RowKey.CompareTo(min) >= 0 && c.RowKey.CompareTo(max) <= 0)
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
                        var cursor = new CursorTableEntity(name);
                        _logger.LogInformation("Creating cursor {Name} to timestamp {Value:O}.", name, cursor.Value);
                        batch.AddEntity(cursor);
                        output.Add(cursor);
                    }
                }
            }

            await batch.SubmitBatchIfNotEmptyAsync();

            return output;
        }

        public async Task<CursorTableEntity> GetOrCreateAsync(string name)
        {
            var table = await GetTableAsync();
            var cursor = await table.GetEntityOrNullAsync<CursorTableEntity>(string.Empty, name);
            if (cursor != null)
            {
                return cursor;
            }
            else
            {
                cursor = new CursorTableEntity(name);
                _logger.LogInformation("Creating cursor {Name} to timestamp {Value:O}.", name, cursor.Value);
                var response = await table.AddEntityAsync(cursor);
                cursor.UpdateETag(response);
                return cursor;
            }
        }

        public async Task UpdateAsync(CursorTableEntity cursor)
        {
            var table = await GetTableAsync();
            _logger.LogInformation("Updating cursor {Name} to timestamp {NewValue:O}.", cursor.Name, cursor.Value);
            var response = await table.UpdateEntityAsync(cursor, cursor.ETag);
            cursor.UpdateETag(response);
        }

        private async Task<TableClient> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.CursorTableName);
        }
    }
}
