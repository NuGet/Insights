using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICatalogLeafToCsvDriver<T> where T : ICsvRecord<T>
    {
        string ResultsContainerName { get; }
        Task<List<T>> ProcessLeafAsync(CatalogLeafItem item);
        List<T> Prune(List<T> records);
    }
}
