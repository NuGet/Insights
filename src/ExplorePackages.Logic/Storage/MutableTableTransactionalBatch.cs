using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

namespace Knapcode.ExplorePackages
{
    public class MutableTableTransactionalBatch : List<TableTransactionalOperation>
    {
        private string _partitionKey;

        public MutableTableTransactionalBatch(TableClient tableClient)
        {
            TableClient = tableClient;
        }

        public TableClient TableClient { get; }

        public void AddEntity<T>(T entity) where T : class, ITableEntity, new()
        {
            SetPartitionKey(entity.PartitionKey);
            Add(new TableTransactionalOperation(entity, x => x.AddEntity(entity)));
        }

        private void SetPartitionKey(string partitionKey)
        {
            if (_partitionKey is not null && _partitionKey != partitionKey)
            {
                throw new InvalidOperationException("Cannot add an entity with a different partition key.");
            }

            _partitionKey = partitionKey;
        }

        public void DeleteEntity(string partitionKey, string rowKey, ETag ifMatch)
        {
            SetPartitionKey(partitionKey);
            Add(new TableTransactionalOperation(entity: null, x => x.DeleteEntity(rowKey, ifMatch)));
        }

        public async Task<Response<TableBatchResponse>> SubmitBatchAsync()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("Cannot submit an empty batch.");
            }

            var batch = TableClient.CreateTransactionalBatch(_partitionKey);
            foreach (var operation in this)
            {
                operation.Act(batch);
            }

            var batchResponse = await batch.SubmitBatchAsync();
            foreach (var operation in this)
            {
                if (operation.Entity != null)
                {
                    var entityResponse = batchResponse.Value.GetResponseForEntity(operation.Entity.RowKey);
                    operation.Entity.UpdateETagAndTimestamp(entityResponse);
                }
            }

            return batchResponse;
        }

        public void UpdateEntity<T>(T entity, ETag ifMatch, TableUpdateMode mode) where T : class, ITableEntity, new()
        {
            SetPartitionKey(entity.PartitionKey);
            Add(new TableTransactionalOperation(entity, x => x.UpdateEntity(entity, ifMatch, mode)));
        }

        public void UpsertEntity<T>(T entity, TableUpdateMode mode) where T : class, ITableEntity, new()
        {
            SetPartitionKey(entity.PartitionKey);
            Add(new TableTransactionalOperation(entity, x => x.UpsertEntity(entity, mode)));
        }
    }
}
