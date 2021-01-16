using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICsvCompactor<T> where T : ICsvRecord<T>, new()
    {
        string ResultsContainerName { get; }
        List<T> Prune(List<T> records);
    }
}
