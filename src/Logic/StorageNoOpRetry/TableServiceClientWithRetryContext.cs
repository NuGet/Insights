// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class TableServiceClientWithRetryContext
    {
        private readonly TableServiceClient _client;
        private readonly ITelemetryClient _telemetryClient;

        public TableServiceClientWithRetryContext(TableServiceClient client, ITelemetryClient telemetryClient)
        {
            _client = client;
            _telemetryClient = telemetryClient;
        }

        public TableClientWithRetryContext GetTableClient(string tableName)
        {
            return new TableClientWithRetryContext(_client.GetTableClient(tableName), _telemetryClient);
        }

        public AsyncPageable<TableItem> QueryAsync(string? filter = null, int? maxPerPage = null, CancellationToken cancellationToken = default)
        {
            return _client.QueryAsync(filter, maxPerPage, cancellationToken);
        }

        public AsyncPageable<TableItem> QueryAsync(
            Expression<Func<TableItem, bool>> filter,
            int? maxPerPage = null,
            CancellationToken cancellationToken = default)
        {
            return _client.QueryAsync(filter, maxPerPage, cancellationToken);
        }

        public Task<Response> DeleteTableAsync(string tableName, CancellationToken cancellationToken = default)
        {
            return _client.DeleteTableAsync(tableName, cancellationToken);
        }
    }
}
