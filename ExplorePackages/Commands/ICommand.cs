using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Commands
{
    public interface ICommand
    {
        void Configure(CommandLineApplication app);
        Task ExecuteAsync(CancellationToken token);
        bool IsDatabaseRequired();
    }
}
