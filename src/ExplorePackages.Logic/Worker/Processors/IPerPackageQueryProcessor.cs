using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public interface IPerPackageQueryProcessor
    {
        Task ProcessAsync(PackageEntity package);
    }
}
