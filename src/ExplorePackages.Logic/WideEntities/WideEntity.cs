using System;
using System.IO;

namespace Knapcode.ExplorePackages.WideEntities
{
    public class WideEntity
    {
        public string PartitionKey { get; }
        public string RowKey { get; }
        public DateTimeOffset Timestamp { get; }
        public string ETag { get; }
        public Stream Stream { get; }
    }
}
