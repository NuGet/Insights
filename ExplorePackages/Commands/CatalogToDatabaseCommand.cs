using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class CatalogToDatabaseCommand : ICommand
    {
        private readonly CatalogToDatabaseProcessor _processor;
        private readonly ExplorePackagesSettings _settings;
        private readonly ILogger _log;

        public CatalogToDatabaseCommand(
            CatalogToDatabaseProcessor processor,
            ExplorePackagesSettings settings,
            ILogger log)
        {
            _processor = processor;
            _settings = settings;
            _log = log;
        }
                
        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            var catalogProcessor = new CatalogProcessorQueue(_processor, _settings, _log);

            await catalogProcessor.ProcessAsync(token);
        }
    }
}
