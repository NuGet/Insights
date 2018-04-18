using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageDependencyService
    {
        private readonly IPackageService _packageService;
        private readonly ILogger _log;

        public PackageDependencyService(IPackageService packageService, ILogger log)
        {
            _packageService = packageService;
            _log = log;
        }

        public async Task AddDependenciesAsync(IReadOnlyList<PackageDependencyGroups> packages)
        {
            // Fetch the package entities.
            var packageEntities = await _packageService.GetPackagesWithDependenciesAsync(packages
                .Select(x => x.Identity)
                .ToList());
            var identityToPackage = packageEntities
                .ToDictionary(x => x.Identity, StringComparer.OrdinalIgnoreCase);

            // Fetch the framework entities.
            var parsedFrameworks = packages
                .SelectMany(x => x
                    .DependencyGroups
                    .Groups
                    .Select(y => new ParsedFramework(y.TargetFramework, y.ParsedTargetFramework?.GetShortFolderName())))
                .Where(x => x.OriginalValue != null && x.Value != null)
                .ToList();
            var originalValueToFramework = await AddOrUpdateFrameworksAsync(parsedFrameworks);

            // Fetch the package registration entities.
            var ids = Enumerable
                .Empty<string>()
                .Concat(packages.SelectMany(x => x.DependencyGroups.Dependencies.Select(y => y.Id)))
                .Concat(packages.SelectMany(x => x.DependencyGroups.Groups.SelectMany(y => y.Dependencies.Select(z => z.Id))))
                .Where(x => StrictPackageIdValidator.IsValid(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var idToPackageRegistration = await _packageService.AddPackageRegistrationsAsync(ids);

            using (var entityContext = new EntityContext())
            {
                foreach (var dependencyGroups in packages)
                {
                    var package = identityToPackage[dependencyGroups.Identity.Value];
                    var packageKey = package.PackageKey;

                    if (package.PackageDependencies == null)
                    {
                        package.PackageDependencies = new List<PackageDependencyEntity>();
                    }

                    var existingDependencies = SortEntities(package.PackageDependencies);

                    var latestDependencies = SortEntities(InitializePackageDependencies(
                        dependencyGroups,
                        packageKey,
                        originalValueToFramework,
                        idToPackageRegistration));

                    latestDependencies = SortEntities(latestDependencies);

                    for (var i = 0; i < latestDependencies.Count; i++)
                    {
                        var latest = latestDependencies[i];

                        if (existingDependencies.Count <= i)
                        {
                            // New dependency added to the end.
                            existingDependencies.Add(latest);
                            entityContext.PackageDependencies.Add(latest);
                        }
                        else
                        {
                            // Existing dependency.
                            var existing = existingDependencies[i];
                            existing.DependencyPackageRegistrationKey = latest.DependencyPackageRegistrationKey;
                            existing.FrameworkKey = latest.FrameworkKey;
                            existing.OriginalVersionRange = latest.OriginalVersionRange;
                            existing.VersionRange = latest.VersionRange;
                        }
                    }

                    for (var i = existingDependencies.Count - 1; i >= latestDependencies.Count; i--)
                    {
                        // Dependencies removed from the end.
                        var existing = existingDependencies[i];
                        existingDependencies.RemoveAt(i);
                        entityContext.PackageDependencies.Remove(existing);
                    }
                }

                var commitStopwatch = Stopwatch.StartNew();
                var changes = await entityContext.SaveChangesAsync();
                _log.LogInformation($"Committed package dependency {changes} changes. {commitStopwatch.ElapsedMilliseconds}ms");
            }
        }

        private List<PackageDependencyEntity> SortEntities(IEnumerable<PackageDependencyEntity> entities)
        {
            return entities
                .OrderBy(x => x.DependencyPackageRegistrationKey)
                .ThenBy(x => x.FrameworkKey)
                .ThenBy(x => x.VersionRange)
                .ToList();
        }

        private List<PackageDependencyEntity> InitializePackageDependencies(
            PackageDependencyGroups package,
            long packageKey,
            IReadOnlyDictionary<string, FrameworkEntity> originalValueToFramework,
            IReadOnlyDictionary<string, PackageRegistrationEntity> idToPackageRegistration)
        {
            var output = new List<PackageDependencyEntity>();

            InitializePackageDependencies(
                output,
                package.DependencyGroups.Dependencies,
                packageKey,
                idToPackageRegistration,
                framework: null);

            foreach (var group in package.DependencyGroups.Groups)
            {
                originalValueToFramework.TryGetValue(group.TargetFramework, out var framework);

                InitializePackageDependencies(
                    output,
                    group.Dependencies,
                    packageKey,
                    idToPackageRegistration,
                    framework);
            }

            return output;
        }

        private void InitializePackageDependencies(
            List<PackageDependencyEntity> output,
            IReadOnlyList<Dependency> dependencies,
            long packageKey,
            IReadOnlyDictionary<string, PackageRegistrationEntity> idToPackageRegistration,
            FrameworkEntity framework)
        {
            foreach (var dependency in dependencies)
            {
                InitializePackageDependency(
                    output,
                    dependency,
                    packageKey,
                    idToPackageRegistration,
                    framework);
            }
        }

        private void InitializePackageDependency(
            List<PackageDependencyEntity> output,
            Dependency dependency,
            long packageKey,
            IReadOnlyDictionary<string, PackageRegistrationEntity> idToPackageRegistration,
            FrameworkEntity framework)
        {
            if (!idToPackageRegistration.TryGetValue(dependency.Id, out var packageRegistration))
            {
                _log.LogWarning(
                    $"Skipping dependency '{dependency.Id}' '{dependency.Version}' since no such package " +
                    $"registration exists.");

                return;
            }

            var dependencyEntity = new PackageDependencyEntity
            {
                ParentPackageKey = packageKey,
                FrameworkKey = framework?.FrameworkKey,
                OriginalVersionRange = dependency.Version,
                VersionRange = dependency.ParsedVersionRange?.ToNormalizedString(),
                DependencyPackageRegistrationKey = packageRegistration.PackageRegistrationKey,
            };

            output.Add(dependencyEntity);
        }

        private async Task<IReadOnlyDictionary<string, FrameworkEntity>> AddOrUpdateFrameworksAsync(IReadOnlyList<ParsedFramework> parsedFrameworks)
        {
            using (var entityContext = new EntityContext())
            {
                var originalValueToValue = parsedFrameworks
                    .GroupBy(x => x.OriginalValue)
                    .ToDictionary(x => x.Key, x => x.First().Value);
                var allOriginalValues = originalValueToValue.Keys.ToList();

                var existingEntities = await entityContext
                    .Frameworks
                    .Where(x => allOriginalValues.Contains(x.OriginalValue))
                    .ToListAsync();

                _log.LogInformation($"Found {existingEntities.Count} existing frameworks.");

                var newOriginalValues = allOriginalValues
                    .Except(existingEntities.Select(x => x.OriginalValue))
                    .ToList();

                var newEntities = new List<FrameworkEntity>();
                foreach (var newOriginalValue in newOriginalValues)
                {
                    var newEntity = new FrameworkEntity
                    {
                        OriginalValue = newOriginalValue,
                        Value = originalValueToValue[newOriginalValue],
                    };

                    entityContext.Frameworks.Add(newEntity);
                    newEntities.Add(newEntity);
                }

                _log.LogInformation($"Adding {newEntities.Count} new frameworks.");

                var commitStopwatch = Stopwatch.StartNew();
                var changes = await entityContext.SaveChangesAsync();
                _log.LogInformation($"Committed {changes} framework changes. {commitStopwatch.ElapsedMilliseconds}ms");

                return existingEntities
                    .Concat(newEntities)
                    .ToDictionary(x => x.OriginalValue);
            }
        }

        private class ParsedFramework
        {
            public ParsedFramework(string originalValue, string value)
            {
                Value = value;
                OriginalValue = originalValue;
            }

            public string Value { get; }
            public string OriginalValue { get; }
        }
    }
}
