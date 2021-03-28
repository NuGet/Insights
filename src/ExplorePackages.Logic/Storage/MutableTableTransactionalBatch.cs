using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

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

        public void AddEntities<T>(IEnumerable<T> entities) where T : class, ITableEntity, new()
        {
            foreach (var entity in entities)
            {
                AddEntity(entity);
            }
        }

        public void AddEntity<T>(T entity) where T : class, ITableEntity, new()
        {
            SetPartitionKey(entity.PartitionKey);
            Add(new TableTransactionalOperation(
                entity,
                batch => batch.AddEntity(entity),
                table => table.AddEntityAsync(entity)));
        }

        public void DeleteEntity(string partitionKey, string rowKey, ETag ifMatch)
        {
            SetPartitionKey(partitionKey);
            Add(new TableTransactionalOperation(
                entity: null,
                batch => batch.DeleteEntity(rowKey, ifMatch),
                table => table.DeleteEntityAsync(partitionKey, rowKey, ifMatch)));
        }

        public void UpdateEntity<T>(T entity, ETag ifMatch, TableUpdateMode mode) where T : class, ITableEntity, new()
        {
            SetPartitionKey(entity.PartitionKey);
            Add(new TableTransactionalOperation(
                entity,
                batch => batch.UpdateEntity(entity, ifMatch, mode),
                table => table.UpdateEntityAsync(entity, ifMatch, mode)));
        }

        public void UpsertEntity<T>(T entity, TableUpdateMode mode) where T : class, ITableEntity, new()
        {
            SetPartitionKey(entity.PartitionKey);
            Add(new TableTransactionalOperation(
                entity,
                batch => batch.UpsertEntity(entity, mode),
                table => table.UpsertEntityAsync(entity, mode)));
        }

        public async Task SubmitBatchIfNotEmptyAsync()
        {
            if (Count == 0)
            {
                return;
            }

            await SubmitBatchAsync();
        }

        public async Task SubmitBatchAsync()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("Cannot submit an empty batch.");
            }
            else if (Count == 1)
            {
                var operation = this[0];
                var response = await operation.SingleAct(TableClient);
                if (operation.Entity != null)
                {
                    operation.Entity.UpdateETagAndTimestamp(response);
                }
            }
            else
            {
                var batch = TableClient.CreateTransactionalBatch(_partitionKey);
                foreach (var operation in this)
                {
                    operation.BatchAct(batch);
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
            }
        }

        private void SetPartitionKey(string partitionKey)
        {
            if (_partitionKey is not null && _partitionKey != partitionKey)
            {
                throw new InvalidOperationException("Cannot add an entity with a different partition key.");
            }

            _partitionKey = partitionKey;
        }
    }
}
