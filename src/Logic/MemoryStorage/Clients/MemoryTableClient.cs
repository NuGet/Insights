// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryTableClient : TableClient
    {
        private readonly MemoryTableStore _store;

        public MemoryTableClient(MemoryTableServiceStore parent, Uri endpoint, string tableName, TokenCredential credential, TableClientOptions options)
            : base(endpoint, tableName, credential, options.AddBrokenTransport())
        {
            _store = parent.GetTable(Name);
        }

        public override Task<Response<TableItem>> CreateIfNotExistsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateIfNotExistsResponse());
        }

        public override Task<Response<IReadOnlyList<Response>>> SubmitTransactionAsync(
            IEnumerable<TableTransactionAction> transactionActions,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SubmitTransactionResponse(transactionActions));
        }

        public override AsyncPageable<T> QueryAsync<T>(
            Expression<Func<T, bool>> filter,
            int? maxPerPage = null,
            IEnumerable<string>? select = null,
            CancellationToken cancellationToken = default)
        {
            return AsyncPageable<T>.FromPages(GetEntityPages(filter, maxPerPage, select));
        }

        public override AsyncPageable<T> QueryAsync<T>(
            string? filter = null,
            int? maxPerPage = null,
            IEnumerable<string>? select = null,
            CancellationToken cancellationToken = default)
        {
            return AsyncPageable<T>.FromPages(GetEntityPages<T>(filter, maxPerPage, select));
        }

        public override Task<Response> DeleteAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeleteResponse());
        }

        public override Task<Response> AddEntityAsync<T>(T entity, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AddEntityResponse(entity));
        }

        public override Task<Response> DeleteEntityAsync(
            string partitionKey,
            string rowKey,
            ETag ifMatch = default,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeleteEntityResponse(partitionKey, rowKey, ifMatch));
        }

        public override Task<Response> UpdateEntityAsync<T>(
            T entity,
            ETag ifMatch,
            TableUpdateMode mode = TableUpdateMode.Merge,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(UpdateEntityResponse(entity, ifMatch, mode));
        }

        public override Task<Response> UpsertEntityAsync<T>(
            T entity,
            TableUpdateMode mode = TableUpdateMode.Merge,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(UpsertEntityResponse(entity, mode));
        }

        public override Task<Response<T>> GetEntityAsync<T>(
            string partitionKey,
            string rowKey,
            IEnumerable<string>? select = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetEntityResponse<T>(partitionKey, rowKey, select));
        }
    }
}
