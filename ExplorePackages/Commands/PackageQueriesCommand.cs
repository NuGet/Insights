using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class PackageQueriesCommand : ICommand
    {
        private readonly PackageQueryProcessor _processor;
        private readonly ILogger _log;

        public PackageQueriesCommand(PackageQueryProcessor processor, ILogger log)
        {
            _processor = processor;
            _log = log;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            var complete = false;
            do
            {
                try
                {
                    await _processor.ProcessAsync(token);
                    complete = true;
                }
                catch (Exception e)
                {
                    _log.LogError("An exception was thrown while processing package queries: " + Environment.NewLine + e);
                }
            }
            while (!complete);
        }

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}
