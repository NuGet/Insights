using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public interface IETagService
    {
        Task<string> GetValueAsync(string name);
        Task ResetValueAsync(string name);
        Task SetValueAsync(string name, string value);
    }
}