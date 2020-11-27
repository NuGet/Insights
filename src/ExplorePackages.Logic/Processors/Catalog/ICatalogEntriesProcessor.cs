using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public interface ICatalogEntriesProcessor
    {
        string CursorName { get; }
        IReadOnlyList<string> DependencyCursorNames { get; }
        Task ProcessAsync(CatalogPageItem page, IReadOnlyList<CatalogLeafItem> leaves);
    }
}
