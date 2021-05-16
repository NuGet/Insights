using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NuGet.Insights.Tool
{
    public interface ICommand
    {
        void Configure(CommandLineApplication app);
        Task ExecuteAsync(CancellationToken token);
    }
}
