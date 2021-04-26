using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    public interface IVersionSetProvider
    {
        Task<IVersionSet> GetAsync();
    }
}