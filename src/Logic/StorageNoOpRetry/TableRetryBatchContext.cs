// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class TableRetryBatchContext : TableRetryContext
    {
        public TableRetryBatchContext(
            Guid clientRequestId,
            TableClientWithRetryContext client,
            string clientRequestIdColumn,
            IReadOnlyList<TableTransactionAction> actions,
            IReadOnlyList<ITableEntityWithClientRequestId> trackedEntities) : base(clientRequestId, client, clientRequestIdColumn)
        {
            Actions = actions;
            TrackedEntities = trackedEntities;
        }

        public IReadOnlyList<TableTransactionAction> Actions { get; }
        public IReadOnlyList<ITableEntityWithClientRequestId> TrackedEntities { get; }
    }
}
