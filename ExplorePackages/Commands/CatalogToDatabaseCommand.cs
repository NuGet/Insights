using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class CatalogToDatabaseCommand : ICommand
    {
        private readonly CatalogToDatabaseProcessor _processor;
        private readonly ILogger _log;

        public CatalogToDatabaseCommand(CatalogToDatabaseProcessor processor, ILogger log)
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
