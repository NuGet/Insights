using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class FindEmptyIdsCommand : ICommand
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly FindEmptyIdsNuspecQuery _processor;
        private readonly ILogger _log;

        public FindEmptyIdsCommand(
            PackagePathProvider pathProvider,
            FindEmptyIdsNuspecQuery processor,
            ILogger log)
        {
            _pathProvider = pathProvider;
            _processor = processor;
            _log = log;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var nuspecProcessor = new NuspecQueryProcessor(
                _pathProvider,
                _processor,
                _log);

            await nuspecProcessor.ProcessAsync(token);
        }
    }
}
