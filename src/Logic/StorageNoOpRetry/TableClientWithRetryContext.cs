// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Azure.Data.Tables.Sas;

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class TableClientWithRetryContext
    {
        private readonly TableClient _client;

        public TableClientWithRetryContext(TableClient client)
        {
            _client = client;
        }

        public Uri Uri => _client.Uri;

        public string Name => _client.Name;

        public Task<Response<TableItem>> CreateIfNotExistsAsync(CancellationToken cancellationToken = default)
        {
            return _client.CreateIfNotExistsAsync(cancellationToken);
        }

        public Task<Response> DeleteAsync(CancellationToken cancellationToken = default)
        {
            return _client.DeleteAsync(cancellationToken);
        }

        public AsyncPageable<T> QueryAsync<T>(
            string? filter = null,
            int? maxPerPage = null,
            IEnumerable<string>? select = null,
            CancellationToken cancellationToken = default) where T : class, ITableEntity
        {
            return _client.QueryAsync<T>(filter, maxPerPage, select, cancellationToken);
        }

        public AsyncPageable<T> QueryAsync<T>(
            Expression<Func<T, bool>> filter,
            int? maxPerPage = null,
            IEnumerable<string>? select = null,
            CancellationToken cancellationToken = default) where T : class, ITableEntity
        {
            return _client.QueryAsync(filter, maxPerPage, select, cancellationToken);
        }

        public Task<Response> DeleteEntityAsync(
            string partitionKey,
            string rowKey,
            ETag ifMatch = default,
            CancellationToken cancellationToken = default)
        {
            return _client.DeleteEntityAsync(partitionKey, rowKey, ifMatch, cancellationToken);
        }

        public Task<Response<T>> GetEntityAsync<T>(
            string partitionKey,
            string rowKey,
            IEnumerable<string>? select = null,
            CancellationToken cancellationToken = default) where T : class, ITableEntity
        {
            return _client.GetEntityAsync<T>(partitionKey, rowKey, select, cancellationToken);
        }

        public async Task<Response> UpsertEntityAsync<T>(
            T entity,
            TableUpdateMode mode = TableUpdateMode.Merge,
            CancellationToken cancellationToken = default) where T : ITableEntity
        {
            return await ExecuteWithClientRequestIdAsync(
                entity,
                () => _client.UpsertEntityAsync(entity, mode, cancellationToken));
        }

        public async Task<Response> UpdateEntityAsync<T>(
            T entity,
            ETag ifMatch,
            TableUpdateMode mode = TableUpdateMode.Merge,
            string clientRequestIdColumn = nameof(ITableEntityWithClientRequestId.ClientRequestId),
            CancellationToken cancellationToken = default) where T : ITableEntity
        {
            return await ExecuteWithEntityRetryContextAsync(
                entity,
                clientRequestIdColumn,
                () => _client.UpdateEntityAsync(entity, ifMatch, mode, cancellationToken));
        }

        public async Task<Response> AddEntityAsync<T>(
            T entity,
            string clientRequestIdColumn = nameof(ITableEntityWithClientRequestId.ClientRequestId),
            CancellationToken cancellationToken = default) where T : ITableEntity
        {
            return await ExecuteWithEntityRetryContextAsync(
                entity,
                clientRequestIdColumn,
                () => _client.AddEntityAsync(entity, cancellationToken));
        }

        public async Task<Response<IReadOnlyList<Response>>> SubmitTransactionAsync(
            IEnumerable<TableTransactionAction> transactionActions,
            string clientRequestIdColumn = nameof(ITableEntityWithClientRequestId.ClientRequestId),
            CancellationToken cancellationToken = default)
        {
            var clientRequestId = Guid.NewGuid();
            var trackedEntities = new List<ITableEntityWithClientRequestId>();
            var transactionActionList = transactionActions.ToList();

            for (var i = 0; i < transactionActionList.Count; i++)
            {
                var action = transactionActionList[i];
                if (action.ActionType != TableTransactionActionType.Delete
                    && action.Entity is ITableEntityWithClientRequestId entityWithClientRequestId)
                {
                    entityWithClientRequestId.ClientRequestId = clientRequestId;
                    trackedEntities.Add(entityWithClientRequestId);
                }
            }

            if (trackedEntities.Count > 0)
            {
                var tableContext = new TableRetryBatchContext(clientRequestId, this, clientRequestIdColumn, transactionActionList, trackedEntities);
                using (StorageNoOpRetryPolicy.CreateScope(tableContext))
                {
                    return await _client.SubmitTransactionAsync(transactionActionList, cancellationToken);
                }
            }

            return await _client.SubmitTransactionAsync(transactionActionList, cancellationToken);
        }

        public TableSasBuilder GetSasBuilder(
            TableSasPermissions permissions,
            DateTimeOffset expiresOn)
        {
            return _client.GetSasBuilder(permissions, expiresOn);
        }

        public Uri GenerateSasUri(TableSasPermissions permissions, DateTimeOffset expiresOn)
        {
            return _client.GenerateSasUri(permissions, expiresOn);
        }

        private async Task<Response> ExecuteWithClientRequestIdAsync<T>(
            T entity,
            Func<Task<Response>> executeAsync)
            where T : ITableEntity
        {
            if (entity is ITableEntityWithClientRequestId entityWithClientRequestId)
            {
                var clientRequestId = Guid.NewGuid();
                entityWithClientRequestId.ClientRequestId = clientRequestId;
                var tableContext = new RetryContext(clientRequestId);
                using (StorageNoOpRetryPolicy.CreateScope(tableContext))
                {
                    return await executeAsync();
                }
            }

            return await executeAsync();
        }

        private async Task<Response> ExecuteWithEntityRetryContextAsync<T>(
            T entity,
            string clientRequestIdColumn,
            Func<Task<Response>> executeAsync)
            where T : ITableEntity
        {
            if (entity is ITableEntityWithClientRequestId entityWithClientRequestId)
            {
                var clientRequestId = Guid.NewGuid();
                entityWithClientRequestId.ClientRequestId = clientRequestId;
                var tableContext = new TableRetryEntityContext(clientRequestId, this, clientRequestIdColumn, entityWithClientRequestId);
                using (StorageNoOpRetryPolicy.CreateScope(tableContext))
                {
                    return await executeAsync();
                }
            }

            return await executeAsync();
        }
    }
}
