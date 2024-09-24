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
        private static readonly MemoryTableServiceStore SharedStore = new MemoryTableServiceStore();

        private readonly TableSharedKeyCredential? _sharedKeyCredential;
        private readonly TokenCredential? _tokenCredential;

        public MemoryTableServiceClient(
            Uri serviceUri,
            TableSharedKeyCredential credential,
            TableClientOptions options) : base(serviceUri, credential, options.AddBrokenTransport())
        {
            _sharedKeyCredential = credential;
            Options = options;
        }

        public MemoryTableServiceClient(
            Uri serviceUri,
            TokenCredential credential,
            TableClientOptions options) : base(serviceUri, credential, options.AddBrokenTransport())
        {
            _tokenCredential = credential;
            Options = options;
        }

        public TableClientOptions Options { get; }
        public MemoryTableServiceStore Store { get; } = SharedStore;

        public override TableClient GetTableClient(
            string tableName)
        {
            if (_sharedKeyCredential is not null)
            {
                return new MemoryTableClient(this, Uri, tableName, _sharedKeyCredential);
            }
            else
            {
                return new MemoryTableClient(this, Uri, tableName, _tokenCredential!);
            }
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
