using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface INuspecQuery
    {
        string Name { get; }
        string CursorName { get; }
        Task<bool> IsMatchAsync(NuspecAndMetadata nuspec);
    }
}
