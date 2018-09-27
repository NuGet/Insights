using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class SandboxCommand : ICommand
    {
        public SandboxCommand()
        {
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            using (var context = new EntityContext())
            {
                var cursor = DateTimeOffset.MinValue.UtcTicks;
                var versionList = new Dictionary<string, SortedSet<NuGetVersion>>(StringComparer.OrdinalIgnoreCase);
                File.Delete("removed.csv");

                while (true)
                {
                    var catalogCommits = await context
                        .CatalogCommits
                        .Include(x => x.CatalogLeaves)
                        .ThenInclude(x => x.CatalogPackage)
                        .ThenInclude(x => x.Package)
                        .Where(x => x.CommitTimestamp > cursor)
                        .OrderBy(x => x.CommitTimestamp)
                        .Take(500)
                        .ToListAsync();

                    Console.WriteLine($"Commits: {catalogCommits.Count}, cursor: {new DateTimeOffset(cursor, TimeSpan.Zero):O}");

                    foreach (var commit in catalogCommits)
                    {
                        var idGroups = commit
                            .CatalogLeaves
                            .GroupBy(x => x.CatalogPackage.Package.Id, StringComparer.OrdinalIgnoreCase);

                        foreach (var idGroup in idGroups)
                        {
                            var isAddToVersions = idGroup
                                .ToLookup(
                                    leafEntity => IsAdd(leafEntity),
                                    leafEntity => NuGetVersion.Parse(leafEntity.CatalogPackage.Package.Version));

                            var highestAdd = isAddToVersions[true].Any() ? isAddToVersions[true].Max() : null;
                            var highestRemove = isAddToVersions[false].Any() ? isAddToVersions[false].Max() : null;
                            
                            if (highestAdd != null && highestRemove != null)
                            {
                                if (highestAdd > highestRemove)
                                {
                                    highestRemove = null;
                                }
                                else if (highestAdd < highestRemove)
                                {
                                    highestAdd = null;
                                }
                            }

                            bool isAdd;
                            NuGetVersion version;
                            if (highestAdd != null && highestRemove == null)
                            {
                                isAdd = true;
                                version = highestAdd;
                            }
                            else if (highestRemove != null && highestAdd == null)
                            {
                                isAdd = false;
                                version = highestRemove;
                            }
                            else
                            {
                                throw new InvalidOperationException($"There should not be deletes and add in the same catalog commit.");
                            }

                            if (!versionList.TryGetValue(idGroup.Key, out var versions))
                            {
                                versions = new SortedSet<NuGetVersion>();
                                versionList.Add(idGroup.Key, versions);
                            }

                            if (versions.Count > 0 && version < versions.Last())
                            {
                                continue;
                            }

                            if (!isAdd && versions.LastOrDefault() == version)
                            {
                                versions.Remove(version);
                                File.AppendAllLines("removed.csv", new[] { $"{new DateTimeOffset(commit.CommitTimestamp, TimeSpan.Zero):O},{idGroup.Key},{version.ToNormalizedString()},{versions.Count}" });
                            }

                            foreach (var group in isAddToVersions)
                            {
                                if (group.Key)
                                {
                                    foreach (var v in group)
                                    {
                                        versions.Add(v);
                                    }
                                }
                                else
                                {
                                    foreach (var v in group)
                                    {
                                        versions.Remove(v);
                                    }
                                }
                            }
                        }
                    }

                    if (catalogCommits.Any())
                    {
                        cursor = catalogCommits.Max(x => x.CommitTimestamp);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private static bool IsAdd(CatalogLeafEntity leafEntity)
        {
            switch (leafEntity.Type)
            {
                case CatalogLeafType.PackageDelete:
                    return false;
                case CatalogLeafType.PackageDetails:
                    return leafEntity.IsListed;
                default:
                    throw new NotImplementedException($"Catalog leaf type {leafEntity.Type} is not supported.");
            }
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
