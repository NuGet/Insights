using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class ShowQueryResultsCommand : ICommand
    {
        private readonly ILogger _log;

        public ShowQueryResultsCommand(ILogger log)
        {
            _log = log;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            if (args.Count < 2)
            {
                Console.WriteLine("A second argument (the query name) is required.");
                return;
            }


            Console.WriteLine("ID\tVersion\tFirst Commit Timestamp\tLast Commit Timestamp");

            var matchesService = new PackageQueryService(_log);

            int count;
            long lastKey = 0;
            do
            {
                var matches = await matchesService.GetMatchedPackagesAsync(args[1], lastKey);
                count = matches.Packages.Count;
                lastKey = matches.LastKey;

                foreach (var match in matches.Packages)
                {
                    Console.Write(match.Id);
                    Console.Write('\t');
                    Console.Write(match.Version);
                    Console.Write('\t');
                    Console.Write(FormatDateTimeOffset(match.FirstCommitTimestamp));
                    Console.Write('\t');
                    Console.Write(FormatDateTimeOffset(match.LastCommitTimestamp));
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

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}
