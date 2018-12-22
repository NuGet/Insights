using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using NuGet.CatalogReader;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class CatalogToNuspecsCommand : ICommand
    {
        private readonly CatalogReader _catalogReader;
        private readonly CursorService _cursorService;
        private readonly CatalogToNuspecsProcessor _processor;

        public CatalogToNuspecsCommand(
            CatalogReader catalogReader,
            CursorService cursorService,
            CatalogToNuspecsProcessor processor)
        {
            _catalogReader = catalogReader;
            _cursorService = cursorService;
            _processor = processor;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var catalogProcessor = new CatalogProcessorQueue(_catalogReader, _cursorService, _processor);
            await catalogProcessor.ProcessAsync(token);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
