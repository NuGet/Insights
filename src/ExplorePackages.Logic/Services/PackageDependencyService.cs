using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageDependencyService
    {
        private readonly IPackageService _packageService;
        private readonly EntityContextFactory _entityContextFactory;
        private readonly ILogger<PackageDependencyService> _logger;

        public PackageDependencyService(
            IPackageService packageService,
            EntityContextFactory entityContextFactory,
            ILogger<PackageDependencyService> logger)
        {
            _packageService = packageService;
            _entityContextFactory = entityContextFactory;
            _logger = logger;
        }

        public async Task<IReadOnlyList<PackageDependencyEntity>> GetDependentPackagesAsync(
            IReadOnlyList<long> packageRegistrationKeys,
            int skip,
            int take)
        {
            using (var entityContext = _entityContextFactory.Get())
            {
                var dependencies = await entityContext
                    .PackageDependencies
                    .Where(x => packageRegistrationKeys.Contains(x.DependencyPackageRegistrationKey))
                    .OrderBy(x => x.PackageDependencyKey)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                var foundPackageRegistrationKeys = dependencies
                    .Select(x => x.DependencyPackageRegistrationKey)
                    .Distinct()
                    .ToList();

                var packageRegistrationKeyToPackageRegistration = await entityContext
                    .PackageRegistrations
                    .Include(x => x.Packages)
                    .ThenInclude(x => x.CatalogPackage)
                    .Where(x => foundPackageRegistrationKeys.Contains(x.PackageRegistrationKey))
                    .ToDictionaryAsync(x => x.PackageRegistrationKey);

                foreach (var dependency in dependencies)    
                {
                    dependency.DependencyPackageRegistration =
                        packageRegistrationKeyToPackageRegistration[dependency.DependencyPackageRegistrationKey];
                }

                return dependencies;
            }
        }

        public async Task UpdateDependencyPackagesAsync(IReadOnlyList<PackageDependencyEntity> dependencies)
        {
            var updates = PrepareUpdates(dependencies);

            await CommitUpdates(updates);
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
                    x => GetVersionToPackage(x));

            var minimumUpdates = new List<KeyValuePair<long, long?>>();
            var bestUpdates = new List<KeyValuePair<long, long?>>();
            var minimumAndBestUpdates = new List<KeyValuePair<long, DependencyPackageKeys>>();

            foreach (var dependency in dependencies)
            {
                var keys = GetDependencyPackageKeys(
                    packageRegistrationKeyToVersionToPackage[dependency.DependencyPackageRegistrationKey],
                    dependency.VersionRange);

                if (dependency.MinimumDependencyPackageKey != keys.MinimumDependencyPackageKey
                    && dependency.BestDependencyPackageKey != keys.BestDependencyPackageKey)
                {
                    minimumAndBestUpdates.Add(KeyValuePairFactory.Create(
                        dependency.PackageDependencyKey,
                        keys));
                }
                else if (dependency.MinimumDependencyPackageKey != keys.MinimumDependencyPackageKey)
                {
                    minimumUpdates.Add(KeyValuePairFactory.Create(
                        dependency.PackageDependencyKey,
                        keys.MinimumDependencyPackageKey));
                }
                else if (dependency.BestDependencyPackageKey != keys.BestDependencyPackageKey)
                {
                    bestUpdates.Add(KeyValuePairFactory.Create(
                        dependency.PackageDependencyKey,
                        keys.BestDependencyPackageKey));
                }
            }

            minimumUpdates.Sort((a, b) => a.Key.CompareTo(b.Key));
            bestUpdates.Sort((a, b) => a.Key.CompareTo(b.Key));
            minimumAndBestUpdates.Sort((a, b) => a.Key.CompareTo(b.Key));

            var changes = minimumUpdates.Count + bestUpdates.Count + minimumAndBestUpdates.Count;

            _logger.LogInformation(
                "Prepared package dependency {Changes} changes. {ElapsedMilliseconds}ms",
                changes,
                prepareStopwatch.ElapsedMilliseconds);

            return new DependencyPackageUpdates(
                minimumUpdates,
                bestUpdates,
                minimumAndBestUpdates);
        }

        private static IReadOnlyDictionary<NuGetVersion, PackageEntity> GetVersionToPackage(PackageRegistrationEntity packageRegistration)
        {
            return packageRegistration
                .Packages
                .Where(x => x.CatalogPackage != null && !x.CatalogPackage.Deleted)
                .ToDictionary(y => NuGetVersion.Parse(y.Version));
        }

        private static DependencyPackageKeys GetDependencyPackageKeys(
            IReadOnlyDictionary<NuGetVersion, PackageEntity> versionToPackage,
            VersionRange parsedVersionRange)
        {
            parsedVersionRange = parsedVersionRange ?? VersionRange.All;

            var minimumMatch = versionToPackage
                .Keys
                .Where(x => parsedVersionRange.Satisfies(x))
                .OrderBy(x => x)
                .FirstOrDefault();
            var bestMatch = parsedVersionRange
                .FindBestMatch(versionToPackage.Keys);

            var minimumKey = minimumMatch != null ? versionToPackage[minimumMatch].PackageKey : (long?)null;
            var bestKey = bestMatch != null ? versionToPackage[bestMatch].PackageKey : (long?)null;

            return new DependencyPackageKeys(minimumKey, bestKey);
        }

        private static DependencyPackageKeys GetDependencyPackageKeys(
            IReadOnlyDictionary<NuGetVersion, PackageEntity> versionToPackage,
            string versionRange)
        {
            VersionRange parsedVersionRange = null;
            if (versionRange != null)
            {
                parsedVersionRange = VersionRange.Parse(versionRange);
            }

            return GetDependencyPackageKeys(versionToPackage, parsedVersionRange);
        }

        private async Task CommitUpdates(DependencyPackageUpdates updates)
        {
            using (var entityContext = _entityContextFactory.Get())
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
                        minimumParameter.Value = (object)update.Value ?? DBNull.Value;
                        changes += await minimumCommand.ExecuteNonQueryAsync();
                    }

                    foreach (var update in updates.BestUpdates)
                    {
                        keyParameter.Value = update.Key;
                        bestParameter.Value = (object)update.Value ?? DBNull.Value;
                        changes += await bestCommand.ExecuteNonQueryAsync();
                    }

                    foreach (var update in updates.MinimumAndBestUpdates)
                    {
                        keyParameter.Value = update.Key;
                        minimumParameter.Value = (object)update.Value.MinimumDependencyPackageKey ?? DBNull.Value;
                        bestParameter.Value = (object)update.Value.BestDependencyPackageKey ?? DBNull.Value;
                        changes += await minimumAndBestCommand.ExecuteNonQueryAsync();
                    }

                    var commitStopwatch = Stopwatch.StartNew();
                    transaction.Commit();
                    _logger.LogInformation(
                        "Committed package dependency {Changes} changes. {commitStopwatch.ElapsedMilliseconds}ms",
                        changes,
                        commitStopwatch.ElapsedMilliseconds);
                }
            }
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

            using (var entityContext = _entityContextFactory.Get())
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
                _logger.LogInformation(
                    "Committed package dependency {Changes} changes. {ElapsedMilliseconds}ms",
                    changes,
                    commitStopwatch.ElapsedMilliseconds);
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
                _logger.LogWarning(
                    "Skipping dependency {Id} {Version} since no such package registration exists.",
                    dependency.Id,
                    dependency.Version);

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
                _logger.LogWarning(
                    "Dependency {Id} (framework '{OriginalFrameworkValue}') is a duplicate. The " +
                    "version range that will be used is '{OriginalVersionRange}'. The " +
                    "version range '{VersionRange}' will be skipped.",
                    dependency.Id,
                    framework.OriginalValue,
                    existingDependencyEntity.OriginalVersionRange,
                    dependency.Version);
            }
        }

        private async Task<IReadOnlyDictionary<string, FrameworkEntity>> AddOrUpdateFrameworksAsync(IReadOnlyList<ParsedFramework> parsedFrameworks)
        {
            using (var entityContext = _entityContextFactory.Get())
            {
                var originalValueToValue = parsedFrameworks
                    .GroupBy(x => x.OriginalValue)
                    .ToDictionary(x => x.Key, x => x.First().Value);
                var allOriginalValues = originalValueToValue.Keys.ToList();

                var existingEntities = await entityContext
                    .Frameworks
                    .Where(x => allOriginalValues.Contains(x.OriginalValue))
                    .ToListAsync();

                _logger.LogInformation("Found {ExistingCount} existing frameworks.", existingEntities.Count);

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

                _logger.LogInformation("Adding {NewCount} new frameworks.", newEntities.Count);

                var commitStopwatch = Stopwatch.StartNew();
                var changes = await entityContext.SaveChangesAsync();
                _logger.LogInformation(
                    "Committed {Changes} framework changes. {ElapsedMilliseconds}ms",
                    changes,
                    commitStopwatch.ElapsedMilliseconds);

                return existingEntities
                    .Concat(newEntities)
                    .ToDictionary(x => x.OriginalValue);
            }
        }

        private class DependencyPackageKeys
        {
            public DependencyPackageKeys(long? minimumDependencyPackageKey, long? bestDependencyPackageKey)
            {
                MinimumDependencyPackageKey = minimumDependencyPackageKey;
                BestDependencyPackageKey = bestDependencyPackageKey;
            }

            public long? MinimumDependencyPackageKey { get; }
            public long? BestDependencyPackageKey { get; }
        }

        private class DependencyPackageUpdates
        {
            public DependencyPackageUpdates(
                IReadOnlyList<KeyValuePair<long, long?>> minimumUpdates,
                IReadOnlyList<KeyValuePair<long, long?>> bestUpdates,
                IReadOnlyList<KeyValuePair<long, DependencyPackageKeys>> minimumAndBestUpdates)
            {
                MinimumUpdates = minimumUpdates;
                BestUpdates = bestUpdates;
                MinimumAndBestUpdates = minimumAndBestUpdates;
            }

            public IReadOnlyList<KeyValuePair<long, long?>> MinimumUpdates { get; }
            public IReadOnlyList<KeyValuePair<long, long?>> BestUpdates { get; }
            public IReadOnlyList<KeyValuePair<long, DependencyPackageKeys>> MinimumAndBestUpdates { get; }
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
