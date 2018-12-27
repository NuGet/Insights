using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
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
