using System;

namespace NuGet.Insights
{
    public interface IAsOfData : IAsyncDisposable
    {
        DateTimeOffset AsOfTimestamp { get; }
    }
}
