using System;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Commands
{
    public class SandboxCommand : ICommand
    {
        private readonly ICommitEnumerator<PackageRegistrationEntity> _packageRegistrationCommitEnumerator;

        public SandboxCommand(
            ICommitEnumerator<PackageRegistrationEntity> packageRegistrationCommitEnumerator)
        {
            _packageRegistrationCommitEnumerator = packageRegistrationCommitEnumerator;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var r = await _packageRegistrationCommitEnumerator.GetCommitsAsync(
                DateTimeOffset.MinValue,
                DateTimeOffset.MaxValue,
                21);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
