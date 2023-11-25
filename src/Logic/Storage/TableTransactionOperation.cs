// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights
{
    public class TableTransactionOperation
    {
        public TableTransactionOperation(ITableEntity entity, TableTransactionAction transactionAction, Func<TableClientWithRetryContext, Task<Response>> singleAct)
        {
            Entity = entity;
            TransactionAction = transactionAction;
            SingleAct = singleAct;
        }

        public ITableEntity Entity { get; }
        public TableTransactionAction TransactionAction { get; }
        public Func<TableClientWithRetryContext, Task<Response>> SingleAct { get; }
    }
}
