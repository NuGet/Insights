using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface INuspecQuery
    {
        string CursorName { get; }
        Task<bool> IsMatchAsync(NuspecAndMetadata nuspec);
    }
}
