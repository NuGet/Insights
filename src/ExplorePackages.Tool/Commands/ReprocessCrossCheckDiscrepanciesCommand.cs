using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class ReprocessCrossCheckDiscrepanciesCommand : ICommand
    {
        private readonly CursorService _cursorService;
        private readonly IEnumerable<IPackageQuery> _queries;
        private readonly PackageQueryProcessor _processor;

        public ReprocessCrossCheckDiscrepanciesCommand(
            CursorService cursorService,
            IEnumerable<IPackageQuery> queries,
            PackageQueryProcessor processor)
        {
            _cursorService = cursorService;
            _queries = queries;
            _processor = processor;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _cursorService.ResetValueAsync(CursorNames.ReprocessPackageQueries);
            var queries = _queries.Where(x => x.Name == PackageQueryNames.HasCrossCheckDiscrepancyPackageQuery).ToList();
            await _processor.ProcessAsync(queries, reprocess: true, batchSize: 5000, token: token);
        }

        public bool IsDatabaseRequired() => true;
        public bool IsReadOnly() => false;
    }
}
