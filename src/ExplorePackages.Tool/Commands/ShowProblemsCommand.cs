using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Tool
{
    public class ShowProblemsCommand : ICommand
    {
        private readonly ProblemService _problemService;
        private readonly ILogger<ShowProblemsCommand> _logger;

        public ShowProblemsCommand(ProblemService problemService, ILogger<ShowProblemsCommand> logger)
        {
            _problemService = problemService;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var problems = await _problemService.GetProblemsAsync();
            _logger.LogInformation("Found {TotalProblemCount} total problems." + Environment.NewLine, problems.Count);

            if (problems.Any())
            {
                // Show count per problem ID.
                var problemIdToCount = problems
                    .GroupBy(x => x.ProblemId)
                    .Select(x => KeyValuePair.Create(x.Key, x.Count()))
                    .OrderByDescending(x => x.Value)
                    .ThenBy(x => x.Key)
                    .ToList();
                var maxCountWidth = problemIdToCount.Max(x => x.Value.ToString().Length);

                _logger.LogInformation("The following problem IDs and their counts were found:" + Environment.NewLine);
                foreach (var pair in problemIdToCount)
                {
                    _logger.LogInformation("  {Count} {ProblemId}", pair.Value.ToString().PadLeft(maxCountWidth), pair.Key);
                }
                Console.WriteLine();

                // Group packages by their set of problem IDs.
                var groups = problems
                    .GroupBy(x => x.PackageIdentity)
                    .Select(x => KeyValuePair.Create(new List<string>(x.Select(y => y.ProblemId).OrderBy(y => y)), x.Key))
                    .GroupBy(x => x.Key, new ListEqualityComparer<string>())
                    .Select(x => KeyValuePair.Create(
                        x.Key,
                        x
                            .Select(y => y.Value)
                            .OrderBy(y => y.Id, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(y => NuGetVersion.Parse(y.Version))
                            .ToList()))
                    .OrderByDescending(x => x.Value.Count)
                    .ThenBy(x => string.Join(Environment.NewLine, x.Key))
                    .ToList();
                _logger.LogInformation("The following packages have problems, grouped by their set of problem IDs:" + Environment.NewLine);
                foreach (var group in groups)
                {
                    foreach (var problemId in group.Key)
                    {
                        _logger.LogInformation("  {ProblemId}", problemId);
                    }

                    foreach (var identity in group.Value)
                    {
                        _logger.LogInformation("    {Id}/{Version}", identity.Id, identity.Version);
                    }
                    Console.WriteLine();
                }
            }
        }

        private class ListEqualityComparer<T> : IEqualityComparer<List<T>>
        {
            public bool Equals(List<T> x, List<T> y)
            {
                return x.SequenceEqual(y);
            }

            public int GetHashCode(List<T> obj)
            {
                var hashCode = new HashCode();
                foreach (var i in obj)
                {
                    hashCode.Add(i);
                }

                return hashCode.ToHashCode();
            }
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
            return false;
        }
    }
}
