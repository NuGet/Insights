using System;

namespace Knapcode.ExplorePackages.WideEntities
{
    public class WideEntityInsertOrReplaceOperation : WideEntityOperation
    {
        public WideEntityInsertOrReplaceOperation(string partitionKey, string rowKey, ReadOnlyMemory<byte> content)
            : base(partitionKey)
        {
            RowKey = rowKey;
            Content = content;
        }

        public string RowKey { get; }
        public ReadOnlyMemory<byte> Content { get; }
    }
}
