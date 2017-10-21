using System;

namespace Knapcode.ExplorePackages.Entities
{
    public class Cursor
    {
        public string Name { get; set; }
        public long Value { get; set; }
        public byte[] RowVersion { get; set; }
        
        public void SetDateTimeOffset(DateTimeOffset value)
        {
            Value = value.UtcTicks;
        }

        public DateTimeOffset GetDateTimeOffset()
        {
            return new DateTimeOffset(Value, TimeSpan.Zero);
        }
    }
}
