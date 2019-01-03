using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class CatalogToDatabaseCommand : ICommand
    {
        private readonly CatalogClient _catalogClient;
        private readonly CursorService _cursorService;
        private readonly CatalogToDatabaseProcessor _processor;
        private readonly ISingletonService _singletonService;
        private readonly ILogger<CatalogProcessorQueue> _logger;

        public CatalogToDatabaseCommand(
            CatalogClient catalogClient,
            CursorService cursorService,
            CatalogToDatabaseProcessor processor,
            ISingletonService singletonService,
            ILogger<CatalogProcessorQueue> logger)
        {
            _catalogClient = catalogClient;
            _cursorService = cursorService;
            _processor = processor;
            _singletonService = singletonService;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var catalogProcessor = new CatalogProcessorQueue(
                _catalogClient,
                _cursorService,
                _processor,
                _singletonService,
                _logger);
            await catalogProcessor.ProcessAsync(token);
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsSingleton() => true;
    }
}
