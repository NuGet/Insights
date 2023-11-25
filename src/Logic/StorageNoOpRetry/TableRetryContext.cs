// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class TableRetryContext : RetryContext
    {
        public TableRetryContext(
            Guid clientRequestId,
            TableClientWithRetryContext client,
            string clientRequestIdColumn) : base(clientRequestId)
        {
            Client = client;
            ClientRequestIdColumn = clientRequestIdColumn;
        }

        public TableClientWithRetryContext Client { get; }
        public string ClientRequestIdColumn { get; }
    }
}
