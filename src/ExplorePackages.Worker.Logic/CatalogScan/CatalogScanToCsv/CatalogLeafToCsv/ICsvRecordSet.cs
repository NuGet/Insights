using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICsvRecordSet<out T> where T : ICsvRecord
    {
        string BucketKey { get; }
        IReadOnlyList<T> Records { get; }
    }
}
