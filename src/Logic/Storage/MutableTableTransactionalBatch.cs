// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Insights
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
                new TableTransactionAction(TableTransactionActionType.Add, entity),
                table => table.AddEntityAsync(entity)));
        }

        public void DeleteEntity(string partitionKey, string rowKey, ETag ifMatch)
        {
            SetPartitionKey(partitionKey);
            Add(new TableTransactionalOperation(
                entity: null,
                new TableTransactionAction(TableTransactionActionType.Delete, new TableEntity(partitionKey, rowKey), ifMatch),
                table => table.DeleteEntityAsync(partitionKey, rowKey, ifMatch)));
        }

        public void UpdateEntity<T>(T entity, ETag ifMatch, TableUpdateMode mode) where T : class, ITableEntity, new()
        {
            SetPartitionKey(entity.PartitionKey);
            var actionType = mode switch
            {
                TableUpdateMode.Merge => TableTransactionActionType.UpdateMerge,
                TableUpdateMode.Replace => TableTransactionActionType.UpdateReplace,
                _ => throw new NotImplementedException(),
            };
            Add(new TableTransactionalOperation(
                entity,
                new TableTransactionAction(actionType, entity, ifMatch),
                table => table.UpdateEntityAsync(entity, ifMatch, mode)));
        }

        public void UpsertEntity<T>(T entity, TableUpdateMode mode) where T : class, ITableEntity, new()
        {
            SetPartitionKey(entity.PartitionKey);
            var actionType = mode switch
            {
                TableUpdateMode.Merge => TableTransactionActionType.UpsertMerge,
                TableUpdateMode.Replace => TableTransactionActionType.UpsertReplace,
                _ => throw new NotImplementedException(),
            };
            Add(new TableTransactionalOperation(
                entity,
                new TableTransactionAction(actionType, entity),
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

                // The SDK swallows HTTP 404 encountered when trying to delete an entity that does not exist.
                // This does not match the batch operation so we manually throw and exception here.
                if (response.Status == (int)HttpStatusCode.NotFound
                    && operation.TransactionAction.ActionType == TableTransactionActionType.Delete)
                {
                    throw new RequestFailedException(response.Status, "The delete failed with a 404.", response.ReasonPhrase, innerException: null);
                }

                if (operation.Entity != null)
                {
                    operation.Entity.UpdateETag(response);
                }
            }
            else
            {
                var batchResponse = await TableClient.SubmitTransactionAsync(this.Select(x => x.TransactionAction));
                for (var i = 0; i < batchResponse.Value.Count; i++)
                {
                    if (this[i].Entity != null)
                    {
                        this[i].Entity.UpdateETag(batchResponse.Value[i]);
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
