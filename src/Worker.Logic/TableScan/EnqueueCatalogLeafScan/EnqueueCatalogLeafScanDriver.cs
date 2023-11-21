// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker.EnqueueCatalogLeafScan
{
    public class EnqueueCatalogLeafScansDriver : ITableScanDriver<CatalogLeafScan>
    {
        private static readonly IList<string> DefaultSelectColumns = new[]
        {
            StorageUtility.PartitionKey,
            StorageUtility.RowKey, // this is the LeafId
            nameof(CatalogLeafScan.StorageSuffix),
            nameof(CatalogLeafScan.ScanId),
            nameof(CatalogLeafScan.PageId),
            nameof(CatalogLeafScan.PackageId),
            nameof(CatalogLeafScan.Min),
            nameof(CatalogLeafScan.Max),
        };

        private readonly SchemaSerializer _serializer;
        private readonly CatalogScanExpandService _expandService;

        public EnqueueCatalogLeafScansDriver(
            SchemaSerializer schemaSerializer,
            CatalogScanExpandService expandService)
        {
            _serializer = schemaSerializer;
            _expandService = expandService;
        }

        public IList<string> SelectColumns => DefaultSelectColumns;

        public Task InitializeAsync(JsonElement? parameters)
        {
            return Task.CompletedTask;
        }

        public async Task ProcessEntitySegmentAsync(string tableName, JsonElement? parameters, IReadOnlyList<CatalogLeafScan> entities)
        {
            var deserializedParameters = (EnqueueCatalogLeafScansParameters)_serializer.Deserialize(parameters.Value).Data;

            if (deserializedParameters.OneMessagePerId)
            {
                entities = entities
                    .GroupBy(x => x.PackageId, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }

            await _expandService.EnqueueLeafScansAsync(entities);
        }
    }
}
