// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker.TableCopy
{
    public class TableCopyDriver<T> : ITableScanDriver<T> where T : class, ITableEntity, new()
    {
        private readonly SchemaSerializer _serializer;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IMessageEnqueuer _enqueuer;

        public TableCopyDriver(
            SchemaSerializer serializer,
            ServiceClientFactory serviceClientFactory,
            IMessageEnqueuer enqueuer)
        {
            _serializer = serializer;
            _serviceClientFactory = serviceClientFactory;
            _enqueuer = enqueuer;
        }

        public IList<string> SelectColumns => StorageUtility.MinSelectColumns;

        public async Task InitializeAsync(JToken parameters)
        {
            var deserializedParameters = DeserializeParameters(parameters);

            var table = (await _serviceClientFactory.GetTableServiceClientAsync())
               .GetTableClient(deserializedParameters.DestinationTableName);

            await table.CreateIfNotExistsAsync(retry: true);
        }

        public async Task ProcessEntitySegmentAsync(string tableName, JToken parameters, IReadOnlyList<T> entities)
        {
            var deserializedParameters = DeserializeParameters(parameters);

            await _enqueuer.EnqueueAsync(
                entities
                    .GroupBy(x => x.PartitionKey)
                    .Select(x => new TableRowCopyMessage<T>
                    {
                        SourceTableName = tableName,
                        DestinationTableName = deserializedParameters.DestinationTableName,
                        PartitionKey = x.Key,
                        RowKeys = x.Select(x => x.RowKey).ToList(),
                    })
                    .ToList(),
                split: m =>
                {
                    if (m.RowKeys.Count <= 2)
                    {
                        return null;
                    }

                    var firstHalf = m.RowKeys.Take(m.RowKeys.Count / 2).ToList();
                    var secondHalf = m.RowKeys.Skip(firstHalf.Count).ToList();

                    return new[]
                    {
                        new TableRowCopyMessage<T>
                        {
                            SourceTableName = m.SourceTableName,
                            DestinationTableName = m.DestinationTableName,
                            PartitionKey = m.PartitionKey,
                            RowKeys = firstHalf,
                        },
                        new TableRowCopyMessage<T>
                        {
                            SourceTableName = m.SourceTableName,
                            DestinationTableName = m.DestinationTableName,
                            PartitionKey = m.PartitionKey,
                            RowKeys = secondHalf,
                        },
                    };
                });
        }

        private TableCopyParameters DeserializeParameters(JToken parameters)
        {
            return (TableCopyParameters)_serializer.Deserialize(parameters).Data;
        }
    }
}
