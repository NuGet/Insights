using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class FindRepositoriesCommand : ICommand
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly FindRepositoriesNuspecProcessor _processor;
        private readonly ILogger _log;

        public FindRepositoriesCommand(
            PackagePathProvider pathProvider,
            FindRepositoriesNuspecProcessor processor,
            ILogger log)
        {
            _pathProvider = pathProvider;
            _processor = processor;
            _log = log;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var nuspecProcessor = new NuspecProcessorQueue(
                _pathProvider,
                _processor,
                _log);

            await nuspecProcessor.ProcessAsync(token);
        }
    }
}
