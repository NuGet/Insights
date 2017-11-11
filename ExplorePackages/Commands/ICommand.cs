using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Commands
{
    public interface ICommand
    {
        Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token);
        bool IsDatabaseRequired(IReadOnlyList<string> args);
    }
}
