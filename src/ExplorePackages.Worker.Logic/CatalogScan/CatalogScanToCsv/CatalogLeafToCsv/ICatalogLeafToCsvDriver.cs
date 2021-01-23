using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICatalogLeafToCsvDriver<T> : ICsvCompactor<T> where T : ICsvRecord<T>, new()
    {
        Task InitializeAsync();
        Task<DriverResult<List<T>>> ProcessLeafAsync(CatalogLeafItem item);
    }
}
