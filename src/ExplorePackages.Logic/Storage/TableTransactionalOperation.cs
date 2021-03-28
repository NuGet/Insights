using System;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages
{
    public class TableTransactionalOperation
    {
        public TableTransactionalOperation(ITableEntity entity, Action<TableTransactionalBatch> batchAct, Func<TableClient, Task<Response>> singleAct)
        {
            Entity = entity;
            BatchAct = batchAct;
            SingleAct = singleAct;
        }

        public ITableEntity Entity { get; }
        public Action<TableTransactionalBatch> BatchAct { get; }
        public Func<TableClient, Task<Response>> SingleAct { get; }
    }
}
