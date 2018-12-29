using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class ShowWeirdMetadataCommand : ICommand
    {
        private readonly PackageQueryService _queryService;
        private readonly NuspecStore _nuspecStore;

        public ShowWeirdMetadataCommand(
            PackageQueryService queryService,
            NuspecStore nuspecStore)
        {
            _queryService = queryService;
            _nuspecStore = nuspecStore;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await ShowMatchedStringCounts(
                PackageQueryNames.FindCaseSensitiveDuplicateMetadataElementsNuspecQuery,
                (package, nuspec) => NuspecUtility.GetDuplicateMetadataElements(nuspec, caseSensitive: true, onlyText: false).Keys);

            await ShowMatchedStringCounts(
                PackageQueryNames.FindCaseSensitiveDuplicateTextMetadataElementsNuspecQuery,
                (package, nuspec) => NuspecUtility.GetDuplicateMetadataElements(nuspec, caseSensitive: false, onlyText: true).Keys);

            await ShowMatchedStringCounts(
                PackageQueryNames.FindCaseInsensitiveDuplicateMetadataElementsNuspecQuery,
                (package, nuspec) => NuspecUtility.GetDuplicateMetadataElements(nuspec, caseSensitive: false, onlyText: false).Keys);

            await ShowMatchedStringCounts(
                PackageQueryNames.FindCaseInsensitiveDuplicateTextMetadataElementsNuspecQuery,
                (package, nuspec) => NuspecUtility.GetDuplicateMetadataElements(nuspec, caseSensitive: false, onlyText: true).Keys);

            await ShowMatchedStringCounts(
                PackageQueryNames.FindNonAlphabetMetadataElementsNuspecQuery,
                (package, nuspec) => NuspecUtility.GetNonAlphabetMetadataElements(nuspec));
        }

        private async Task ShowMatchedStringCounts(string queryName, Func<PackageEntity, XDocument, IEnumerable<string>> getStrings)
        {
            var matches = new Dictionary<string, HashSet<string>>();
            await ProcessMatchedNuspecsAsync(
                queryName,
                (package, nuspec) =>
                {
                    foreach (var value in getStrings(package, nuspec))
                    {
                        var fixedValue = value ?? "(null)";
                        if (!matches.ContainsKey(fixedValue))
                        {
                            matches[fixedValue] = new HashSet<string>(new[] { package.Identity }, StringComparer.OrdinalIgnoreCase);
                        }
                        else
                        {
                            matches[fixedValue].Add(package.Identity);
                        }
                    }
                });
            Console.WriteLine(queryName);
            if (matches.Any())
            {
                var maxCountWidth = matches.Values.Max(x => x.Count.ToString().Length);
                var maxKeyWidth = matches.Keys.Max(x => x.Length);
                foreach (var pair in matches.OrderByDescending(x => x.Value.Count))
                {
                    Console.WriteLine($"  {pair.Value.Count.ToString().PadLeft(maxCountWidth)} {pair.Key.PadRight(maxKeyWidth)} ({GetPreview(pair.Value)})");
                }
            }
            Console.WriteLine();
        }

        private string GetPreview(HashSet<string> identities)
        {
            if (identities.Count <= 3)
            {
                return string.Join(", ", identities);
            }
            else
            {
                return string.Join(", ", identities.Take(3)) + "...";
            }
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
                    var nuspecContext = await _nuspecStore.GetNuspecContextAsync(match.Id, match.Version);
                    processNuspec(match, nuspecContext.Document);
                }
            }
            while (count > 0);
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsReadOnly() => true;
    }
}
