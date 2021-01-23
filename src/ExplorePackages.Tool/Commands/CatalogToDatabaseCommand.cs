using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Tool
{
    public class CatalogToDatabaseCommand : ICommand
    {
        private readonly CatalogClient _catalogClient;
        private readonly CursorService _cursorService;
        private readonly CatalogToDatabaseProcessor _processor;
        private readonly ISingletonService _singletonService;
        private readonly IOptions<ExplorePackagesEntitiesSettings> _options;
        private readonly ILogger<CatalogProcessorQueue> _logger;

        public CatalogToDatabaseCommand(
            CatalogClient catalogClient,
            CursorService cursorService,
            CatalogToDatabaseProcessor processor,
            ISingletonService singletonService,
            IOptions<ExplorePackagesEntitiesSettings> options,
            ILogger<CatalogProcessorQueue> logger)
        {
            _catalogClient = catalogClient;
            _cursorService = cursorService;
            _processor = processor;
            _singletonService = singletonService;
            _options = options;
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
                _options,
                _logger);
            await catalogProcessor.ProcessAsync();
        }

        public bool IsInitializationRequired()
        {
            return true;
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }

        public bool IsSingleton()
        {
            return true;
        }
    }
}
