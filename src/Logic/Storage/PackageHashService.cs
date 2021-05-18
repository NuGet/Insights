// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace NuGet.Insights
{
    public class PackageHashService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public PackageHashService(
            ServiceClientFactory serviceClientFactory,
            IOptions<NuGetInsightsSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await (await GetTableAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task SetHashesAsync(CatalogLeafItem item, HashOutput hashes)
        {
            var table = await GetTableAsync();
            var pk = GetPartitionKey(item.PackageId);
            var rk = GetRowKey(item.PackageVersion);

            var entity = new HashesEntity
            {
                PartitionKey = pk,
                RowKey = rk,
                CommitTimestamp = item.CommitTimestamp,
                Available = !item.IsPackageDelete(),
            };

            if (entity.Available)
            {
                entity.MD5 = hashes.MD5;
                entity.SHA1 = hashes.SHA1;
                entity.SHA256 = hashes.SHA256;
                entity.SHA512 = hashes.SHA512;
            }

            await table.UpsertEntityAsync(entity, mode: TableUpdateMode.Replace);
        }

        public async Task<HashOutput> GetHashesOrNullAsync(string id, string version)
        {
            var table = await GetTableAsync();
            var pk = GetPartitionKey(id);
            var rk = GetRowKey(version);

            /// This will throw an exception if the entity does not exist. This is expected because the called should
            /// have a dependency on the cursor of the driver that populates this table (i.e. the caller of the
            /// <see cref="SetHashesAsync(CatalogLeafItem, HashOutput)"/> method).
            HashesEntity entity = await table.GetEntityAsync<HashesEntity>(pk, rk);
            if (!entity.Available)
            {
                return null;
            }

            return entity;
        }

        private async Task<TableClient> GetTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.PackageHashesTableName);
        }

        private static string GetPartitionKey(string id)
        {
            return id.ToLowerInvariant();
        }

        private static string GetRowKey(string version)
        {
            return NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
        }

        private class HashesEntity : HashOutput, ITableEntity
        {
            public string PartitionKey { get; set; }
            public string RowKey { get; set; }
            public DateTimeOffset? Timestamp { get; set; }
            public ETag ETag { get; set; }

            public DateTimeOffset CommitTimestamp { get; set; }
            public bool Available { get; set; }
        }
    }
}
