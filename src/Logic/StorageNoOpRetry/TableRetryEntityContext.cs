// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class TableRetryEntityContext : TableRetryContext
    {
        public TableRetryEntityContext(
            Guid clientRequestId,
            TableClientWithRetryContext client,
            string clientRequestIdColumn,
            ITableEntityWithClientRequestId entity) : base(clientRequestId, client, clientRequestIdColumn)
        {
            Entity = entity;
        }

        public ITableEntityWithClientRequestId Entity { get; }
    }
}
