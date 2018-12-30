using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class ShowQueryResultsCommand : ICommand
    {
        private readonly PackageQueryService _queryService;
        private CommandArgument _queryNameArgument;

        public ShowQueryResultsCommand(PackageQueryService queryService)
        {
            _queryService = queryService;
        }

        public void Configure(CommandLineApplication app)
        {
            _queryNameArgument = app.Argument(
                "query name",
                "The name of the query to show results for.",
                x => x.IsRequired());
        }

        private string QueryName => _queryNameArgument?.Value;

        public async Task ExecuteAsync(CancellationToken token)
        {
            Console.WriteLine("ID\tVersion\tFirst Commit Timestamp\tLast Commit Timestamp");
            
            int count;
            long lastKey = 0;
            do
            {
                var matches = await _queryService.GetMatchedPackagesAsync(QueryName, lastKey);
                count = matches.Packages.Count;
                lastKey = matches.LastKey;

                foreach (var match in matches.Packages)
                {
                    Console.Write(match.PackageRegistration);
                    Console.Write('\t');
                    Console.Write(match.Version);
                    Console.Write('\t');
                    Console.Write(FormatDateTimeOffset(match.CatalogPackage.FirstCommitTimestamp));
                    Console.Write('\t');
                    Console.Write(FormatDateTimeOffset(match.CatalogPackage.LastCommitTimestamp));
                    Console.WriteLine();
                }
            }
            while (count > 0);
        }

        private string FormatDateTimeOffset(long? ticks)
        {
            if (!ticks.HasValue)
            {
                return string.Empty;
            }

            var dateTimeOffset = new DateTimeOffset(ticks.Value, TimeSpan.Zero);
            return dateTimeOffset.ToString("G", CultureInfo.InvariantCulture);
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsSingleton() => false;
    }
}
