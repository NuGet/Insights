using System.Collections.Generic;

namespace NuGet.Insights.Worker
{
    public interface ICsvRecordSet<out T> where T : ICsvRecord
    {
        string BucketKey { get; }
        IReadOnlyList<T> Records { get; }
    }
}
