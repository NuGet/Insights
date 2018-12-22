using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class SandboxCommand : ICommand
    {
        private readonly EntityContextFactory _entityContextFactory;

        public SandboxCommand(EntityContextFactory entityContextFactory)
        {
            _entityContextFactory = entityContextFactory;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            using (var entityContext = _entityContextFactory.Get())
            {
                var package = await entityContext.Packages.FirstOrDefaultAsync();
            }
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
