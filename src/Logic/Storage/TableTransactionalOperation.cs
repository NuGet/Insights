// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Insights
{
    public class TableTransactionalOperation
    {
        public TableTransactionalOperation(ITableEntity entity, TableTransactionAction transactionAction, Func<TableClient, Task<Response>> singleAct)
        {
            Entity = entity;
            TransactionAction = transactionAction;
            SingleAct = singleAct;
        }

        public ITableEntity Entity { get; }
        public TableTransactionAction TransactionAction { get; }
        public Func<TableClient, Task<Response>> SingleAct { get; }
    }
}
