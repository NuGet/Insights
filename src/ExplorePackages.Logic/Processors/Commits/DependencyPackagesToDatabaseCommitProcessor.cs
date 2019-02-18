using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class DependencyPackagesToDatabaseCommitProcessor : ICommitProcessor<PackageRegistrationEntity, PackageDependencyEntity, DependencyPackagesToDatabaseCommitProcessor.ProgressToken>
    {
        private readonly PackageDependencyService _packageDependencyService;
        private readonly IBatchSizeProvider _batchSizeProvider;
        private readonly ILogger<DependencyPackagesToDatabaseCommitProcessor> _logger;

        public DependencyPackagesToDatabaseCommitProcessor(
            PackageDependencyService packageDependencyService,
            IBatchSizeProvider batchSizeProvider,
            ILogger<DependencyPackagesToDatabaseCommitProcessor> logger)
        {
            _packageDependencyService = packageDependencyService;
            _batchSizeProvider = batchSizeProvider;
            _logger = logger;
        }

        public string CursorName => CursorNames.DependencyPackagesToDatabase;

        public IReadOnlyList<string> DependencyCursorNames => new[]
        {
            CursorNames.DependenciesToDatabase,
        };

        public int BatchSize => _batchSizeProvider.Get(BatchSizeType.DependencyPackagesToDatabase_PackageRegistrations);

        public string SerializeProgressToken(ProgressToken progressToken)
        {
            if (progressToken == null)
            {
                return null;
            }

            return JsonConvert.SerializeObject(progressToken);
        }

        public ProgressToken DeserializeProgressToken(string serializedProgressToken)
        {
            if (serializedProgressToken == null)
            {
                return null;
            }

            var json = JToken.Parse(serializedProgressToken);
            if (json.Type != JTokenType.Object)
            {
                return null;
            }

            var jsonObject = (JObject)json;
            if (jsonObject.Property(nameof(ProgressToken.SchemaVersion))?.Value.Type != JTokenType.Integer)
            {
                return null;
            }

            if (jsonObject.Value<int>(nameof(ProgressToken.SchemaVersion)) != ProgressToken.SchemaVersion2)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<ProgressToken>(serializedProgressToken);
        }

        public async Task<ItemBatch<PackageDependencyEntity, ProgressToken>> InitializeItemsAsync(
            IReadOnlyList<PackageRegistrationEntity> entities,
            ProgressToken progressToken,
            CancellationToken token)
        {
            var packageRegistrationKeyToId = entities
                .ToDictionary(x => x.PackageRegistrationKey, x => x.Id);

            var allPackageRegistrationKeys = packageRegistrationKeyToId
                .Keys
                .OrderBy(x => x)
                .ToList();

            if (progressToken != null)
            {
                // Verify the package registration keys match those stored in the progress token.
                if (!progressToken.AllPackageRegistrationKeys.OrderBy(x => x).SequenceEqual(allPackageRegistrationKeys))
                {
                    throw new ArgumentException("The provided progress token does not match the provided package registration entites.");
                }
            }

            // Initialize the progress token, if necessary.
            if (progressToken == null)
            {
                // If the batch size very small, process the package registrations one by one.
                if (allPackageRegistrationKeys.Count <= 10)
                {
                    progressToken = new ProgressToken(
                        allPackageRegistrationKeys,
                        packageRegistrationKeyIndex: 0,
                        afterKey: 0);
                }
                else
                {
                    progressToken = new ProgressToken(
                        allPackageRegistrationKeys,
                        packageRegistrationKeyIndex: null,
                        afterKey: 0);
                }
            }

            // Determine the list of package registration keys that will be queried.
            IReadOnlyList<long> packageRegistrationKeys;
            if (progressToken.PackageRegistrationKeyIndex.HasValue)
            {
                packageRegistrationKeys = new[] { progressToken.AllPackageRegistrationKeys[progressToken.PackageRegistrationKeyIndex.Value] };
            }
            else
            {
                packageRegistrationKeys = progressToken.AllPackageRegistrationKeys;
            }

            // Query for dependents.
            var packagesBatchSize = _batchSizeProvider.Get(BatchSizeType.DependencyPackagesToDatabase_Packages);

            _logger.LogInformation(
                "Fetching up to {Take} dependent packages for {Count} package registrations after package dependency key {AfterKey}.",
                packagesBatchSize,
                packageRegistrationKeys.Count,
                progressToken.AfterKey);

            var sw = Stopwatch.StartNew();
            var dependents = await _packageDependencyService.GetDependentPackagesAsync(
                packageRegistrationKeys,
                progressToken.AfterKey,
                take: packagesBatchSize);
            sw.Stop();

            _logger.LogInformation(
                "Fetched {Count} dependent packages. Took {Elapsed}ms.",
                dependents.Count,
                sw.ElapsedMilliseconds);

            var topDependencyPairs = dependents
                .GroupBy(x => x.DependencyPackageRegistrationKey)
                .ToDictionary(
                    x => packageRegistrationKeyToId[x.Key],
                    x => x.Select(y => y.ParentPackageKey).Distinct().Count())
                .OrderByDescending(x => x.Value)
                .Take(5)
                .ToList();

            if (topDependencyPairs.Any())
            {
                var width = topDependencyPairs.Max(x => x.Value.ToString().Length);

                _logger.LogInformation(
                    $"Top dependencies:{Environment.NewLine}" +
                    string.Join(
                        Environment.NewLine,
                        topDependencyPairs.Select((x, i) => $"  {x.Value.ToString().PadLeft(width)} {x.Key}")));
            }

            // Build the next progress token.
            bool hasMoreItems;
            ProgressToken nextProgressToken;
            if (dependents.Count >= packagesBatchSize)
            {
                /// We got a full batch back. Simply move the progress token's <see cref="ProgressToken.AfterKey"/>
                /// forward.
                hasMoreItems = true;
                nextProgressToken = new ProgressToken(
                    progressToken.AllPackageRegistrationKeys,
                    progressToken.PackageRegistrationKeyIndex,
                    dependents.Max(x => x.PackageDependencyKey));
            }
            else
            {
                // We got a partial batch back.
                if (progressToken.PackageRegistrationKeyIndex.HasValue)
                {
                    // We are processing package registrations one by one.
                    if (progressToken.PackageRegistrationKeyIndex.Value < progressToken.AllPackageRegistrationKeys.Count - 1)
                    {
                        // There are more package registrations to process, so we move to the next one.
                        hasMoreItems = true;
                        nextProgressToken = new ProgressToken(
                            progressToken.AllPackageRegistrationKeys,
                            progressToken.PackageRegistrationKeyIndex.Value + 1,
                            afterKey: 0);
                    }
                    else
                    {
                        // There are no more package registrations to process, so we are one.
                        hasMoreItems = false;
                        nextProgressToken = null;
                    }
                }
                else
                {
                    // We are not processing package registrations one by one, so we are done.
                    hasMoreItems = false;
                    nextProgressToken = null;
                }
            }

            return new ItemBatch<PackageDependencyEntity, ProgressToken>(dependents, hasMoreItems, nextProgressToken);
        }

        public async Task ProcessBatchAsync(IReadOnlyList<PackageDependencyEntity> batch)
        {
            await _packageDependencyService.UpdateDependencyPackagesAsync(batch);
        }

        public class ProgressToken
        {
            internal const int SchemaVersion2 = 2;

            [JsonConstructor]
            public ProgressToken(IReadOnlyList<long> allPackageRegistrationKeys, int? packageRegistrationKeyIndex, long afterKey)
            {
                AllPackageRegistrationKeys = allPackageRegistrationKeys ?? throw new ArgumentNullException(nameof(allPackageRegistrationKeys));
                PackageRegistrationKeyIndex = packageRegistrationKeyIndex;
                AfterKey = afterKey;
            }

            public int SchemaVersion => SchemaVersion2;
            
            public IReadOnlyList<long> AllPackageRegistrationKeys { get; }
            public int? PackageRegistrationKeyIndex { get; }
            public long AfterKey { get; }
        }
    }
}
