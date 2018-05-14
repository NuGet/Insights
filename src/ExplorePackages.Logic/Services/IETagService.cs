using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IETagService
    {
        Task<string> GetValueAsync(string name);
        Task ResetValueAsync(string name);
        Task SetValueAsync(string name, string value);
    }
}