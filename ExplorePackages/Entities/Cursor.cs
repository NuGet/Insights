using System;

namespace Knapcode.ExplorePackages.Entities
{
    public class Cursor
    {
        public string Name { get; set; }
        public DateTime Value { get; set; }
        public byte[] RowVersion { get; set; }
    }
}
