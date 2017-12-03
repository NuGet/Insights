using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;

namespace Knapcode.ExplorePackages.Commands
{
    public class ShowWeirdDependenciesCommand : ICommand
    {
        private readonly PackageQueryService _queryService;
        private readonly PackagePathProvider _pathProvider;

        public ShowWeirdDependenciesCommand(
            PackageQueryService queryService,
            PackagePathProvider pathProvider)
        {
            _queryService = queryService;
            _pathProvider = pathProvider;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            await ShowMatchedStringCounts(
                PackageQueryNames.FindInvalidDependencyIdNuspecQuery,
                (package, nuspec) => NuspecUtility.GetInvalidDependencyIds(nuspec));

            await ShowMatchedStringCounts(
                PackageQueryNames.FindUnsupportedDependencyTargetFrameworkNuspecQuery,
                (package, nuspec) => NuspecUtility.GetUnsupportedDependencyTargetFrameworks(nuspec));

            await ShowMatchedStringCounts(
                PackageQueryNames.FindMixedDependencyGroupStylesNuspecQuery,
                (package, nuspec) =>
                {
                    if (NuspecUtility.HasMixedDependencyGroupStyles(nuspec))
                    {
                        return new[] { package.Identity };
                    }
                    else
                    {
                        return new string[0];
                    }
                });
        }

        private async Task ShowMatchedStringCounts(string queryName, Func<PackageEntity, XDocument, IEnumerable<string>> getStrings)
        {
            var matches = new Dictionary<string, int>();
            await ProcessMatchedNuspecsAsync(
                queryName,
                (package, nuspec) =>
                {
                    foreach (var value in getStrings(package, nuspec))
                    {
                        if (!matches.ContainsKey(value))
                        {
                            matches[value] = 1;
                        }
                        else
                        {
                            matches[value]++;
                        }
                    }
                });
            var maxWidth = matches.Values.Max(x => x.ToString().Length);
            Console.WriteLine(queryName);
            foreach (var pair in matches.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"  {pair.Value.ToString().PadLeft(maxWidth)} {pair.Key}");
            }
            Console.WriteLine();
        }

        private async Task ProcessMatchedNuspecsAsync(string queryName, Action<PackageEntity, XDocument> processNuspec)
        {
            int count;
            long lastKey = 0;
            do
            {
                var matches = await _queryService.GetMatchedPackagesAsync(queryName, lastKey);
                count = matches.Packages.Count;
                lastKey = matches.LastKey;

                foreach (var match in matches.Packages)
                {
                    var path = _pathProvider.GetLatestNuspecPath(match.PackageRegistration.Id, match.Version);
                    XDocument nuspec;
                    using (var stream = File.OpenRead(path))
                    {
                        nuspec = XmlUtility.LoadXml(stream);
                    }

                    processNuspec(match, nuspec);
                }
            }
            while (count > 0);
        }

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}
