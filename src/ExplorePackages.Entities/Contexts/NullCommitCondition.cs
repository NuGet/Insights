using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public class NullCommitCondition : ICommitCondition
    {
        public static NullCommitCondition Instance { get; } = new NullCommitCondition();

        public Task VerifyAsync()
        {
            return Task.CompletedTask;
        }
    }
}
