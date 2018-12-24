using System;

namespace Knapcode.ExplorePackages.Entities
{
    public class LeaseEntity
    {
        public long LeaseKey { get; set; }
        public string Name { get; set; }
        public DateTimeOffset? End { get; set; }
        public byte[] RowVersion { get; set; }
    }
}
