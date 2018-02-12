using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.CatalogReader;

namespace Knapcode.ExplorePackages.Logic
{
    public interface ICatalogEntriesProcessor
    {
        string CursorName { get; }
        IReadOnlyList<string> DependencyCursorNames { get; }
        Task ProcessAsync(CatalogPageEntry page, IReadOnlyList<CatalogEntry> leaves);
    }
}
