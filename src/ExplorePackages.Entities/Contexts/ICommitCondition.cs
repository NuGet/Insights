using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public interface ICommitCondition
    {
        Task VerifyAsync();
    }
}
