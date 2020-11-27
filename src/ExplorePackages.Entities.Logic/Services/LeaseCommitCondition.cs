using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public class LeaseCommitCondition : ICommitCondition
    {
        private readonly ISingletonService _singletonLeaseService;

        public LeaseCommitCondition(ISingletonService singletonLeaseService)
        {
            _singletonLeaseService = singletonLeaseService;
        }

        public async Task VerifyAsync()
        {
            await _singletonLeaseService.RenewAsync();
        }
    }
}
