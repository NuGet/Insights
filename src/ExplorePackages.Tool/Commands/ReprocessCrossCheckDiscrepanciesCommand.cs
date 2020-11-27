using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool
{
    public class ReprocessCrossCheckDiscrepanciesCommand : ICommand
    {
        private readonly CursorService _cursorService;
        private readonly PackageQueryFactory _packageQueryFactory;
        private readonly PackageQueryCollector _processor;

        public ReprocessCrossCheckDiscrepanciesCommand(
            CursorService cursorService,
            PackageQueryFactory packageQueryFactory,
            PackageQueryCollector processor)
        {
            _cursorService = cursorService;
            _packageQueryFactory = packageQueryFactory;
            _processor = processor;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _cursorService.ResetValueAsync(CursorNames.ReprocessPackageQueries);
            var queries = _packageQueryFactory
                .Get()
                .Where(x => x.Name == PackageQueryNames.HasCrossCheckDiscrepancyPackageQuery)
                .ToList();

            await _processor.ProcessAsync(
                queries,
                reprocess: true);
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsSingleton() => true;
    }
}
