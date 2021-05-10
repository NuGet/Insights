using System;

namespace Knapcode.ExplorePackages
{
    public interface IAsOfData : IAsyncDisposable
    {
        DateTimeOffset AsOfTimestamp { get; }
    }
}
