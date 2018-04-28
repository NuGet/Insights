using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Support;
using Microsoft.EntityFrameworkCore;
using NuGet.Common;
using NuGet.Versioning;

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

        public async Task<IReadOnlyList<PackageDependencyEntity>> GetDependentPackagesAsync(
            IReadOnlyList<long> packageRegistrationKeys,
            int skip,
            int take)
        {
            using (var entityContext = new EntityContext())
            {
                return await entityContext
                    .PackageDependencies
                    .Include(x => x.DependencyPackageRegistration)
                    .ThenInclude(x => x.Packages)
                    .Where(x => packageRegistrationKeys.Contains(x.DependencyPackageRegistration.PackageRegistrationKey))
                    .OrderBy(x => x.PackageDependencyKey)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
        }

        public async Task UpdateDependencyPackagesAsync(IReadOnlyList<PackageDependencyEntity> dependencies)
        {
            var updates = PrepareUpdates(dependencies);

            await CommitUpdates(updates);
        }

        private async Task CommitUpdates(DependencyPackageUpdates updates)
        {
            using (var entityContext = new EntityContext())
            using (var connection = entityContext.Database.GetDbConnection())
            {
                await connection.OpenAsync();

                var changes = 0;
                using (var transaction = connection.BeginTransaction())
                using (var minimumCommand = connection.CreateCommand())
                using (var bestCommand = connection.CreateCommand())
                using (var minimumAndBestCommand = connection.CreateCommand())
                {
                    minimumCommand.CommandText = @"
                        UPDATE PackageDependencies
                        SET MinimumDependencyPackageKey = @Minimum
                        WHERE PackageDependencyKey = @Key";
                    bestCommand.CommandText = @"
                        UPDATE PackageDependencies
                        SET BestDependencyPackageKey = @Best
                        WHERE PackageDependencyKey = @Key";
                    minimumAndBestCommand.CommandText = @"
                        UPDATE PackageDependencies
                        SET MinimumDependencyPackageKey = @Minimum, BestDependencyPackageKey = @Best
                        WHERE PackageDependencyKey = @Key";

                    var minimumParameter = minimumCommand.CreateParameter();
                    minimumParameter.ParameterName = "Minimum";
                    minimumParameter.DbType = DbType.Int64;
                    minimumCommand.Parameters.Add(minimumParameter);
                    minimumAndBestCommand.Parameters.Add(minimumParameter);

                    var bestParameter = bestCommand.CreateParameter();
                    bestParameter.ParameterName = "Best";
                    bestParameter.DbType = DbType.Int64;
                    bestCommand.Parameters.Add(bestParameter);
                    minimumAndBestCommand.Parameters.Add(bestParameter);

                    var keyParameter = minimumAndBestCommand.CreateParameter();
                    keyParameter.ParameterName = "Key";
                    keyParameter.DbType = DbType.Int64;
                    minimumCommand.Parameters.Add(keyParameter);
                    bestCommand.Parameters.Add(keyParameter);
                    minimumAndBestCommand.Parameters.Add(keyParameter);

                    foreach (var update in updates.MinimumUpdates)
                    {
                        keyParameter.Value = update.Key;
                        minimumParameter.Value = update.Value;
                        changes += await minimumCommand.ExecuteNonQueryAsync();
                    }

                    foreach (var update in updates.BestUpdates)
                    {
                        keyParameter.Value = update.Key;
                        bestParameter.Value = update.Value;
                        changes += await bestCommand.ExecuteNonQueryAsync();
                    }

                    foreach (var update in updates.MinimumAndBestUpdates)
                    {
                        keyParameter.Value = update.Key;
                        minimumParameter.Value = update.Value.Item1;
                        bestParameter.Value = update.Value.Item2;
                        changes += await minimumAndBestCommand.ExecuteNonQueryAsync();
                    }

                    var commitStopwatch = Stopwatch.StartNew();
                    transaction.Commit();
                    _log.LogInformation($"Committed package dependency {changes} changes. {commitStopwatch.ElapsedMilliseconds}ms");
                }
            }
        }

        private DependencyPackageUpdates PrepareUpdates(IReadOnlyList<PackageDependencyEntity> dependencies)
        {
            var prepareStopwatch = Stopwatch.StartNew();
            var packageRegistrationKeyToVersionToPackage = dependencies
                .Select(x => x.DependencyPackageRegistration)
                .GroupBy(x => x.PackageRegistrationKey)
                .Select(x => x.First())
                .ToDictionary(
                    x => x.PackageRegistrationKey,
                    x => x
                        .Packages
                        .ToDictionary(y => NuGetVersion.Parse(y.Version)));

            var minimumUpdates = new List<KeyValuePair<long, long?>>();
            var bestUpdates = new List<KeyValuePair<long, long?>>();
            var minimumAndBestUpdates = new List<KeyValuePair<long, Tuple<long?, long?>>>();

            foreach (var dependency in dependencies)
            {
                var versionToPackage = packageRegistrationKeyToVersionToPackage[dependency.DependencyPackageRegistrationKey];

                VersionRange parsedVersionRange;
                if (dependency.VersionRange != null)
                {
                    parsedVersionRange = VersionRange.Parse(dependency.VersionRange);
                }
                else
                {
                    parsedVersionRange = VersionRange.All;
                }

                var minimumMatch = versionToPackage
                    .Keys
                    .Where(x => parsedVersionRange.Satisfies(x))
                    .OrderBy(x => x)
                    .FirstOrDefault();
                var bestMatch = parsedVersionRange
                    .FindBestMatch(versionToPackage.Keys);

                var minimumDependentPackageKey = minimumMatch != null ? versionToPackage[minimumMatch].PackageKey : (long?)null;
                var bestDependentPackageKey = bestMatch != null ? versionToPackage[bestMatch].PackageKey : (long?)null;

                if (dependency.MinimumDependencyPackageKey != minimumDependentPackageKey
                    && dependency.BestDependencyPackageKey != bestDependentPackageKey)
                {
                    minimumAndBestUpdates.Add(KeyValuePair.Create(
                        dependency.PackageDependencyKey,
                        Tuple.Create(minimumDependentPackageKey, bestDependentPackageKey)));
                }
                else if (dependency.MinimumDependencyPackageKey != minimumDependentPackageKey)
                {
                    minimumUpdates.Add(KeyValuePair.Create(
                        dependency.PackageDependencyKey,
                        minimumDependentPackageKey));
                }
                else if (dependency.BestDependencyPackageKey != bestDependentPackageKey)
                {
                    bestUpdates.Add(KeyValuePair.Create(
                        dependency.PackageDependencyKey,
                        bestDependentPackageKey));
                }
            }

            minimumUpdates.Sort((a, b) => a.Key.CompareTo(b.Key));
            bestUpdates.Sort((a, b) => a.Key.CompareTo(b.Key));
            minimumAndBestUpdates.Sort((a, b) => a.Key.CompareTo(b.Key));

            var changes = minimumUpdates.Count + bestUpdates.Count + minimumAndBestUpdates.Count;

            _log.LogInformation($"Prepared package dependency {changes} changes. {prepareStopwatch.ElapsedMilliseconds}ms");

            return new DependencyPackageUpdates(
                minimumUpdates,
                bestUpdates,
                minimumAndBestUpdates);
        }

        private class DependencyPackageUpdates
        {
            public DependencyPackageUpdates(
                IReadOnlyList<KeyValuePair<long, long?>> minimumUpdates,
                IReadOnlyList<KeyValuePair<long, long?>> bestUpdates,
                IReadOnlyList<KeyValuePair<long, Tuple<long?, long?>>> minimumAndBestUpdates)
            {
                MinimumUpdates = minimumUpdates;
                BestUpdates = bestUpdates;
                MinimumAndBestUpdates = minimumAndBestUpdates;
            }

            public IReadOnlyList<KeyValuePair<long, long?>> MinimumUpdates { get; }
            public IReadOnlyList<KeyValuePair<long, long?>> BestUpdates { get; }
            public IReadOnlyList<KeyValuePair<long, Tuple<long?, long?>>> MinimumAndBestUpdates { get; }
        }

        public async Task AddDependenciesAsync(IReadOnlyList<PackageDependencyGroups> packages)
        {
            // Fetch the package entities.
            var identities = packages
                .Select(x => x.Identity)
                .ToList();
            var identityToPackage = await GetIdentityToPackage(identities);

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
            var idToPackageRegistration = await _packageService.AddPackageRegistrationsAsync(ids, includePackages: false);

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
                            entityContext.PackageDependencies.Attach(existing);
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
                        entityContext.PackageDependencies.Attach(existing);
                        entityContext.PackageDependencies.Remove(existing);
                    }
                }

                var commitStopwatch = Stopwatch.StartNew();
                var changes = await entityContext.SaveChangesAsync();
                _log.LogInformation($"Committed package dependency {changes} changes. {commitStopwatch.ElapsedMilliseconds}ms");
            }
        }

        private async Task<IReadOnlyDictionary<string, PackageEntity>> GetIdentityToPackage(
            IReadOnlyList<PackageIdentity> identities)
        {
            var packageEntities = await _packageService.GetPackagesWithDependenciesAsync(identities);

            return packageEntities
                .ToDictionary(x => x.Identity, StringComparer.OrdinalIgnoreCase);
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
            var output = new Dictionary<UniqueDependency, PackageDependencyEntity>();

            InitializePackageDependencies(
                output,
                package.DependencyGroups.Dependencies,
                packageKey,
                idToPackageRegistration,
                framework: null);

            foreach (var group in package.DependencyGroups.Groups)
            {
                FrameworkEntity framework = null;
                if (!string.IsNullOrEmpty(group.TargetFramework))
                {
                    originalValueToFramework.TryGetValue(group.TargetFramework, out framework);
                }

                InitializePackageDependencies(
                    output,
                    group.Dependencies,
                    packageKey,
                    idToPackageRegistration,
                    framework);
            }

            return output.Values.ToList();
        }

        private void InitializePackageDependencies(
            Dictionary<UniqueDependency, PackageDependencyEntity> output,
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
            Dictionary<UniqueDependency, PackageDependencyEntity> output,
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

            var key = new UniqueDependency(
                dependencyEntity.DependencyPackageRegistrationKey,
                dependencyEntity.FrameworkKey);

            if (!output.TryGetValue(key, out var existingDependencyEntity))
            {
                output.Add(key, dependencyEntity);
            }
            else
            {
                _log.LogWarning(
                    $"Dependency {dependency.Id} (framework '{framework.OriginalValue}') is a duplicate. The " +
                    $"version range that will be used is '{existingDependencyEntity.OriginalVersionRange}'. The " +
                    $"version range '{dependency.Version}' will be skipped.");
            }
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

        private class UniqueDependency : IEquatable<UniqueDependency>
        {
            public UniqueDependency(long dependencyPackageRegistrationKey, long? frameworkKey)
            {
                DependencyPackageRegistrationKey = dependencyPackageRegistrationKey;
                FrameworkKey = frameworkKey;
            }

            public long DependencyPackageRegistrationKey { get; }
            public long? FrameworkKey { get; }

            public override bool Equals(object obj)
            {
                return Equals(obj as UniqueDependency);
            }

            public bool Equals(UniqueDependency other)
            {
                if (other == null)
                {
                    return false;
                }

                return DependencyPackageRegistrationKey == other.DependencyPackageRegistrationKey
                    && FrameworkKey == other.FrameworkKey;
            }

            public override int GetHashCode()
            {
                var hashCode = 2107211798;
                hashCode = hashCode * -1521134295 + DependencyPackageRegistrationKey.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<long?>.Default.GetHashCode(FrameworkKey);
                return hashCode;
            }
        }
    }
}
