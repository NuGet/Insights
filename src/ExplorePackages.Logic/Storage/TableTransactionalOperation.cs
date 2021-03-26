using System;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages
{
    public class TableTransactionalOperation
    {
        public TableTransactionalOperation(ITableEntity entity, Action<TableTransactionalBatch> act)
        {
            Entity = entity;
            Act = act;
        }

        public ITableEntity Entity { get; }
        public Action<TableTransactionalBatch> Act { get; }
    }
}
