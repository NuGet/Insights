using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class CatalogToNuspecsCommand : ICommand
    {
        private readonly CatalogToNuspecsProcessor _processor;
        private readonly ILogger _log;

        public CatalogToNuspecsCommand(CatalogToNuspecsProcessor processor, ILogger log)
        {
            _processor = processor;
            _log = log;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var catalogProcessor = new CatalogProcessorQueue(_processor, _log);

            await catalogProcessor.ProcessAsync(token);
        }
    }
}
