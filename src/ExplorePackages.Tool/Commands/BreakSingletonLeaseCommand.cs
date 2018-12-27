using System;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class BreakSingletonLeaseCommand : ICommand
    {
        private readonly ISingletonService _singletonService;

        public BreakSingletonLeaseCommand(ISingletonService singletonService)
        {
            _singletonService = singletonService;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _singletonService.BreakAsync();
        }

        public bool IsDatabaseRequired() => true;
        public bool IsReadOnly() => true;
    }
}
