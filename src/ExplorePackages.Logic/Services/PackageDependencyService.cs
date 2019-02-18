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
        private readonly ICommitCondition _commitCondition;
        private readonly EntityContextFactory _entityContextFactory;
        private readonly ILogger<PackageDependencyService> _logger;

        public PackageDependencyService(
            IPackageService packageService,
            ICommitCondition commitCondition,
            EntityContextFactory entityContextFactory,
            ILogger<PackageDependencyService> logger)
        {
            _packageService = packageService;
            _commitCondition = commitCondition;
            _entityContextFactory = entityContextFactory;
            _logger = logger;
        }

        public async Task<IReadOnlyList<PackageDependencyEntity>> GetDependentPackagesAsync(
            IReadOnlyList<long> packageRegistrationKeys,
            long afterKey,
            int take)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                _logger.LogInformation(
                    "Fetching up to {Take} dependent packages for {Count} package registrations after package dependency key {AfterKey}.",
                    take,
                    packageRegistrationKeys.Count,
                    afterKey);

                var dependencies = await entityContext
                    .PackageDependencies
                    .Where(x => packageRegistrationKeys.Contains(x.DependencyPackageRegistrationKey))
                    .OrderBy(x => x.PackageDependencyKey)
                    .Where(x => x.PackageDependencyKey > afterKey)
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
            using (var entityContext = await _entityContextFactory.GetAsync())
            using (var connection = entityContext.Database.GetDbConnection())
            {
                await _commitCondition.VerifyAsync();
                await connection.OpenAsync();

                var changes = 0;
                using (var transaction = connection.BeginTransaction())
                using (var minimumCommand = connection.CreateCommand())
                using (var bestCommand = connection.CreateCommand())
                using (var minimumAndBestCommand = connection.CreateCommand())
                {
                    minimumCommand.Transaction = transaction;
                    bestCommand.Transaction = transaction;
                    minimumAndBestCommand.Transaction = transaction;

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

                    var keyForMinimumParameter = minimumCommand.CreateParameter();
                    keyForMinimumParameter.ParameterName = "Key";
                    keyForMinimumParameter.DbType = DbType.Int64;
                    minimumCommand.Parameters.Add(keyForMinimumParameter);

                    var minimumForMinimumParameter = minimumCommand.CreateParameter();
                    minimumForMinimumParameter.ParameterName = "Minimum";
                    minimumForMinimumParameter.DbType = DbType.Int64;
                    minimumCommand.Parameters.Add(minimumForMinimumParameter);

                    var keyForBestParameter = bestCommand.CreateParameter();
                    keyForBestParameter.ParameterName = "Key";
                    keyForBestParameter.DbType = DbType.Int64;
                    bestCommand.Parameters.Add(keyForBestParameter);

                    var bestForBestParameter = bestCommand.CreateParameter();
                    bestForBestParameter.ParameterName = "Best";
                    bestForBestParameter.DbType = DbType.Int64;
                    bestCommand.Parameters.Add(bestForBestParameter);

                    var keyForMinimumAndBestParameter = minimumAndBestCommand.CreateParameter();
                    keyForMinimumAndBestParameter.ParameterName = "Key";
                    keyForMinimumAndBestParameter.DbType = DbType.Int64;
                    minimumAndBestCommand.Parameters.Add(keyForMinimumAndBestParameter);

                    var minimumForMinimumAndBestParameter = minimumAndBestCommand.CreateParameter();
                    minimumForMinimumAndBestParameter.ParameterName = "Minimum";
                    minimumForMinimumAndBestParameter.DbType = DbType.Int64;
                    minimumAndBestCommand.Parameters.Add(minimumForMinimumAndBestParameter);

                    var bestForMinimumAndBestParameter = minimumAndBestCommand.CreateParameter();
                    bestForMinimumAndBestParameter.ParameterName = "Best";
                    bestForMinimumAndBestParameter.DbType = DbType.Int64;
                    minimumAndBestCommand.Parameters.Add(bestForMinimumAndBestParameter);

                    foreach (var update in updates.MinimumUpdates)
                    {
                        keyForMinimumParameter.Value = update.Key;
                        minimumForMinimumParameter.Value = (object)update.Value ?? DBNull.Value;
                        changes += await minimumCommand.ExecuteNonQueryAsync();
                    }

                    foreach (var update in updates.BestUpdates)
                    {
                        keyForBestParameter.Value = update.Key;
                        bestForBestParameter.Value = (object)update.Value ?? DBNull.Value;
                        changes += await bestCommand.ExecuteNonQueryAsync();
                    }

                    foreach (var update in updates.MinimumAndBestUpdates)
                    {
                        keyForMinimumAndBestParameter.Value = update.Key;
                        minimumForMinimumAndBestParameter.Value = (object)update.Value.MinimumDependencyPackageKey ?? DBNull.Value;
                        bestForMinimumAndBestParameter.Value = (object)update.Value.BestDependencyPackageKey ?? DBNull.Value;
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
            var idToPackageRegistration = await _packageService.AddPackageRegistrationsAsync(
                ids,
                includePackages: false,
                includeCatalogPackageRegistrations: false);

            using (var entityContext = await _entityContextFactory.GetAsync())
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
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var originalValueToValue = parsedFrameworks
                    .GroupBy(x => x.OriginalValue, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);
                var allOriginalValues = originalValueToValue.Keys.ToList();

                var existingEntities = await entityContext
                    .Frameworks
                    .Where(x => allOriginalValues.Contains(x.OriginalValue))
                    .ToListAsync();

                _logger.LogInformation("Found {ExistingCount} existing frameworks.", existingEntities.Count);

                var newOriginalValues = allOriginalValues
                    .Except(existingEntities.Select(x => x.OriginalValue), StringComparer.OrdinalIgnoreCase)
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
                    .ToDictionary(x => x.OriginalValue, StringComparer.OrdinalIgnoreCase);
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
