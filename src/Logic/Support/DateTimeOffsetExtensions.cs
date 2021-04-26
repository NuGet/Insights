using System;

namespace Knapcode.ExplorePackages
{
    public static class DateTimeOffsetExtensions
    {
        public static string ToZulu(this DateTimeOffset input)
        {
            return input.ToUniversalTime().ToString("O").Replace("+00:00", "Z");
        }
    }
}
