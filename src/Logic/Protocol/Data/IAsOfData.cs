using System;

namespace Knapcode.ExplorePackages
{
    public interface IAsOfData
    {
        DateTimeOffset AsOfTimestamp { get; }
    }
}
