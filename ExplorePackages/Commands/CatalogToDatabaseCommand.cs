using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using NuGet.CatalogReader;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class CatalogToDatabaseCommand : ICommand
    {
        private readonly CatalogReader _catalogReader;
        private readonly CatalogToDatabaseProcessor _processor;
        private readonly ILogger _log;

        public CatalogToDatabaseCommand(
            CatalogReader catalogReader,
            CatalogToDatabaseProcessor processor,
            ILogger log)
        {
            _catalogReader = catalogReader;
            _processor = processor;
            _log = log;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var catalogProcessor = new CatalogProcessorQueue(_catalogReader, _processor, _log);

            await catalogProcessor.ProcessAsync(token);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
