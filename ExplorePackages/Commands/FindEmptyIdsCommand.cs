using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class FindEmptyIdsCommand : ICommand
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly FindEmptyIdsNuspecProcessor _processor;
        private readonly ILogger _log;

        public FindEmptyIdsCommand(
            PackagePathProvider pathProvider,
            FindEmptyIdsNuspecProcessor processor,
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
