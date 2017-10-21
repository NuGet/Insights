using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface INuspecProcessor
    {
        Task ProcessAsync(NuspecAndMetadata nuspec);
    }
}
