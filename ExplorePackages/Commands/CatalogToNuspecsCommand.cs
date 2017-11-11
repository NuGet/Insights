using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class CatalogToNuspecsCommand : ICommand
    {
        private readonly CatalogToNuspecsProcessor _processor;
        private readonly ExplorePackagesSettings _settings;
        private readonly ILogger _log;

        public CatalogToNuspecsCommand(
            CatalogToNuspecsProcessor processor,
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

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}
