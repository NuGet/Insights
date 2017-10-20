using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Commands
{
    public interface ICommand
    {
        Task ExecuteAsync(CancellationToken token);
    }
}
