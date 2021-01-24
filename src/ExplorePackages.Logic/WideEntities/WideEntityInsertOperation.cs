using System;

namespace Knapcode.ExplorePackages.WideEntities
{
    public class WideEntityInsertOperation : WideEntityOperation
    {
        public WideEntityInsertOperation(string partitionKey, string rowKey, ReadOnlyMemory<byte> content)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
            Content = content;
        }

        public string PartitionKey { get; }
        public string RowKey { get; }
        public ReadOnlyMemory<byte> Content { get; }
    }
}
