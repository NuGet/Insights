// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryTableServiceClient : TableServiceClient
    {
        private readonly MemoryTableServiceStore _store;
        private readonly TokenCredential _credential;
        private readonly TableClientOptions _options;

        public MemoryTableServiceClient(MemoryTableServiceStore store) : this(
            store,
            StorageUtility.GetTableEndpoint(StorageUtility.MemoryStorageAccountName),
            MemoryTokenCredential.Instance,
            new TableClientOptions().AddBrokenTransport())
        {
        }

        private MemoryTableServiceClient(MemoryTableServiceStore store, Uri serviceUri, TokenCredential credential, TableClientOptions options)
            : base(serviceUri, credential, options.AddBrokenTransport())
        {
            _store = store;
            _credential = credential;
            _options = options;
        }

        public override TableClient GetTableClient(
            string tableName)
        {
            return new MemoryTableClient(_store, Uri, tableName, _credential, _options);
        }

        public override AsyncPageable<TableItem> QueryAsync(
            string? filter = null,
            int? maxPerPage = null,
            CancellationToken cancellationToken = default)
        {
            return AsyncPageable<TableItem>.FromPages(GetTablePages(filter, maxPerPage));
        }

        public override AsyncPageable<TableItem> QueryAsync(
            Expression<Func<TableItem, bool>> filter,
            int? maxPerPage = null,
            CancellationToken cancellationToken = default)
        {
            return AsyncPageable<TableItem>.FromPages(GetTablePages(filter, maxPerPage));
        }

        public override Task<Response> DeleteTableAsync(string tableName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeleteTableResponse(tableName));
        }
    }
}
