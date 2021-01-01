using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker;
using Knapcode.ExplorePackages.Worker.FindLatestLeaves;
using Knapcode.ExplorePackages.Worker.FindPackageAssemblies;
using Knapcode.ExplorePackages.Worker.RunRealRestore;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.Versioning;
using NuGetPackageIdentity = NuGet.Packaging.Core.PackageIdentity;

namespace Knapcode.ExplorePackages.Tool
{
    public class SandboxCommand : ICommand
    {
        private readonly CatalogScanService _catalogScanService;
        private readonly IWorkerQueueFactory _workerQueueFactory;
        private readonly CursorStorageService _cursorStorageService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly LatestPackageLeafStorageService _latestPackageLeafStorageService;
        private readonly AppendResultStorageService _appendResultStorageService;
        private readonly IMessageProcessor<RunRealRestoreMessage> _messageProcessor;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly SchemaSerializer _serializer;
        private readonly StorageLeaseService _storageLeaseService;
        private readonly ICatalogLeafToCsvDriver<PackageAssembly> _findPackageAssembliesDriver;
        private readonly TablePrefixScanner _tablePrefixScanner;
        private readonly TableScanService<LatestPackageLeaf> _tableScanService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<SandboxCommand> _logger;

        public SandboxCommand(
            CatalogScanService catalogScanService,
            IWorkerQueueFactory workerQueueFactory,
            CursorStorageService cursorStorageService,
            CatalogScanStorageService catalogScanStorageService,
            LatestPackageLeafStorageService latestPackageLeafStorageService,
            AppendResultStorageService appendResultStorageService,
            IMessageProcessor<RunRealRestoreMessage> messageProcessor,
            MessageEnqueuer messageEnqueuer,
            ServiceClientFactory serviceClientFactory,
            SchemaSerializer serializer,
            StorageLeaseService storageLeaseService,
            ICatalogLeafToCsvDriver<PackageAssembly> findPackageAssembliesDriver,
            TablePrefixScanner tablePrefixScanner,
            TableScanService<LatestPackageLeaf> tableScanService,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<SandboxCommand> logger)
        {
            _catalogScanService = catalogScanService;
            _workerQueueFactory = workerQueueFactory;
            _cursorStorageService = cursorStorageService;
            _catalogScanStorageService = catalogScanStorageService;
            _latestPackageLeafStorageService = latestPackageLeafStorageService;
            _appendResultStorageService = appendResultStorageService;
            _messageProcessor = messageProcessor;
            _messageEnqueuer = messageEnqueuer;
            _serviceClientFactory = serviceClientFactory;
            _serializer = serializer;
            _storageLeaseService = storageLeaseService;
            _findPackageAssembliesDriver = findPackageAssembliesDriver;
            _tablePrefixScanner = tablePrefixScanner;
            _tableScanService = tableScanService;
            _options = options;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public abstract class StepCollection
        {
            public abstract void Add(int? parentId, TablePrefixScanStep step);
            public abstract void AddRange(int? parentId, IEnumerable<TablePrefixScanStep> steps);
            public abstract bool Any();
            public abstract (int? ParentId, TablePrefixScanStep Step) RemoveNext();
        }

        public class DFS : StepCollection
        {
            private readonly Stack<(int? ParentId, TablePrefixScanStep Step)> _data = new Stack<(int? ParentId, TablePrefixScanStep Step)>();

            public override void Add(int? parentId, TablePrefixScanStep step) => _data.Push((parentId, step));

            public override void AddRange(int? parentId, IEnumerable<TablePrefixScanStep> steps)
            {
                foreach (var step in steps.Reverse())
                {
                    Add(parentId, step);
                }
            }

            public override bool Any() => _data.Any();
            public override (int? ParentId, TablePrefixScanStep Step) RemoveNext() => _data.Pop();
        }

        public class BFS : StepCollection
        {
            private readonly Queue<(int? ParentId, TablePrefixScanStep Step)> _data = new Queue<(int? ParentId, TablePrefixScanStep Step)>();

            public override void Add(int? parentId, TablePrefixScanStep step) => _data.Enqueue((parentId, step));

            public override void AddRange(int? parentId, IEnumerable<TablePrefixScanStep> steps)
            {
                foreach (var step in steps)
                {
                    Add(parentId, step);
                }
            }

            public override bool Any() => _data.Any();
            public override (int? ParentId, TablePrefixScanStep Step) RemoveNext() => _data.Dequeue();
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            // await _catalogScanService.InitializeAsync();

            // await _catalogScanService.RequeueAsync("CatalogScan-FindPackageAssemblies", "08585928640987077276-2zbm6qzw65ne5dlde242pezrcm");

            var stepCollections = new Func<StepCollection>[] { () => new BFS(), () => new DFS() };
            foreach (var getRemainingSteps in stepCollections)
            {
                var account = CloudStorageAccount.DevelopmentStorageAccount;
                var client = account.CreateCloudTableClient();
                var table = client.GetTableReference("example1");
                var queryParameters = new TableQueryParameters(table, StorageUtility.MinSelectColumns, 2);

                var remainingSteps = getRemainingSteps();
                var firstPrefix = "";
                remainingSteps.Add(null, new TablePrefixScanStart(queryParameters, firstPrefix));

                var completedSteps = new List<object>();
                var entityCount = 0;
                var lastThreshold = 0;
                var nextStepId = 1;
                while (remainingSteps.Any())
                {
                    var currentStep = remainingSteps.RemoveNext();
                    var currentStepId = nextStepId++;

                    List<TablePrefixScanStep> nextSteps;
                    object stepData;
                    switch (currentStep.Step)
                    {
                        case TablePrefixScanStart start:
                            nextSteps = _tablePrefixScanner.Start(start);
                            stepData = new { Type = "Start", start.Depth, start.PartitionKeyPrefix };
                            break;
                        case TablePrefixScanEntitySegment<TableEntity> entitySegment:
                            nextSteps = new List<TablePrefixScanStep>();
                            var first = entitySegment.Entities.First();
                            var last = entitySegment.Entities.Last();
                            entityCount += entitySegment.Entities.Count;
                            stepData = new { Type = "EntitySegment", entitySegment.Depth, First = new { first.PartitionKey, first.RowKey }, Last = new { last.PartitionKey, last.RowKey }, entitySegment.Entities.Count };
                            break;
                        case TablePrefixScanPartitionKeyQuery partitionKeyQuery:
                            nextSteps = await _tablePrefixScanner.ExecutePartitionKeyQueryAsync<TableEntity>(partitionKeyQuery);
                            stepData = new { Type = "PartitionKeyQuery", partitionKeyQuery.Depth, partitionKeyQuery.PartitionKey, partitionKeyQuery.RowKeySkip };
                            break;
                        case TablePrefixScanPrefixQuery prefixQuery:
                            nextSteps = await _tablePrefixScanner.ExecutePrefixQueryAsync<TableEntity>(prefixQuery);
                            stepData = new { Type = "PrefixQuery", prefixQuery.Depth, prefixQuery.PartitionKeyPrefix, prefixQuery.PartitionKeyLowerBound };
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    remainingSteps.AddRange(currentStepId, nextSteps);

                    completedSteps.Add(new
                    {
                        Id = currentStepId,
                        currentStep.ParentId,
                        Timestamp = DateTimeOffset.UtcNow.ToString("O"),
                        Data = stepData,
                    });

                    var roundedDownEntityCount = entityCount - (entityCount % 1000);
                    if (roundedDownEntityCount > lastThreshold)
                    {
                        lastThreshold = roundedDownEntityCount;
                        Console.WriteLine(lastThreshold);
                    }
                }

                File.WriteAllText($"steps-{firstPrefix}-{remainingSteps.GetType().Name}.json", JsonConvert.SerializeObject(completedSteps));
            }

            // await _catalogScanService.InitializeAsync();
            // var max = DateTimeOffset.Parse("2020-12-29T19:08:26.8305274+00:00");
            // await _catalogScanService.StartFindLatestLeavesAsync(prefix: "00", max);
            // await _catalogScanService.StartFindLatestLeavesAsync(prefix: "01", max);
            // await _catalogScanService.StartFindLatestLeavesAsync(prefix: "02", max);
            // await _catalogScanService.StartFindLatestLeavesAsync(prefix: "03", max);
            // await _catalogScanService.StartFindLatestLeavesAsync(prefix: "04", max);
            // await _catalogScanService.StartFindLatestLeavesAsync(prefix: "05", max);
            // await _catalogScanService.StartFindLatestLeavesAsync(prefix: "06", max);
            // await _catalogScanService.StartFindLatestLeavesAsync(prefix: "07", max);
            // await _catalogScanService.StartFindLatestLeavesAsync(prefix: "08", max);
            // await _catalogScanService.StartFindLatestLeavesAsync(prefix: "09", max);

            /*
	        await _tableCopyEnqueuer.StartPrefixScanAsync(
                "latestleavesperf10",
                "latestleavesperf10pscopy",
                partitionKeyPrefix: string.Empty,
                takeCount: StorageUtility.MaxTakeCount);
            */

            /*
            await _tableCopyEnqueuer.StartSerialAsync(
                "latestleavesperf02",
                "latestleavesperf02serialcopy");
            */

            /*
            await _tableCopyEnqueuer.StartPrefixScanAsync(
                _options.Value.LatestLeavesTableName,
                _options.Value.LatestLeavesTableName + "pscopy",
                partitionKeyPrefix: string.Empty,
                takeCount: StorageUtility.MaxBatchSize);
            */

            /*
            await _tableCopyEnqueuer.StartSerialAsync(
                _options.Value.LatestLeavesTableName,
                _options.Value.LatestLeavesTableName + "serialcopy");
            */

            /*
            var tableClient = _serviceClientFactory.GetStorageAccount().CreateCloudTableClient();
            var comparison = new EntityComparisonEnumerable<LatestPackageLeaf>(
                new EntityEnumerable<LatestPackageLeaf>(tableClient.GetTableReference("latestleaves"), takeCount: null),
                new EntityEnumerable<LatestPackageLeaf>(tableClient.GetTableReference("latestleavespscopy"), takeCount: null));
            var enumerator = comparison.GetEnumerator();

            var count = 0;
            while (await enumerator.MoveNextAsync())
            {
                count++;
                var notSame = enumerator.Current.Type != EntityComparisonType.Same;
                if (notSame || count % 1000 == 0)
                {
                    var entity = enumerator.Current.Left?.RawEntity ?? enumerator.Current.Right.RawEntity;
                    var currentColor = Console.ForegroundColor;
                    Console.ForegroundColor = notSame ? ConsoleColor.Red : ConsoleColor.Green;
                    Console.WriteLine($"{count}: {entity.PartitionKey} {entity.RowKey} - {enumerator.Current.Type}");
                    Console.ForegroundColor = currentColor;
                }
            }
            */

            /*
            var increment = 0;

            var leaves = new[]
            {
                new CatalogLeafItem
                {
                    Url = "https://api.nuget.org/v3/catalog0/data/2018.10.13.13.46.44/cntk.deps.cudnn.2.4.0.json",
                    Type = CatalogLeafType.PackageDetails,
                    PackageId = "CNTK.Deps.cuDNN",
                    PackageVersion = "2.4.0",
                    CommitId = "02e08d75-c4b1-4d90-af1f-42f3dddaee30",
                    CommitTimestamp = DateTimeOffset.Parse("2018-10-13T13:46:44.773813Z").AddDays(increment),
                },
                new CatalogLeafItem
                {
                    Url = "https://api.nuget.org/v3/catalog0/data/2018.08.23.17.47.42/cntk.deps.cudnn.2.6.0-rc0.dev20180823.json",
                    Type = CatalogLeafType.PackageDetails,
                    PackageId = "CNTK.Deps.cuDNN",
                    PackageVersion = "2.6.0-rc0.dev20180823",
                    CommitId = "271c3ece-38ca-4045-8d79-82a6c90d1877",
                    CommitTimestamp = DateTimeOffset.Parse("2018-08-23T17:47:42.033489Z").AddDays(increment),
                },
                new CatalogLeafItem
                {
                    Url = "https://api.nuget.org/v3/catalog0/data/2018.12.10.07.25.55/opencvcuda-debug.redist.3.1.0.json",
                    Type = CatalogLeafType.PackageDetails,
                    PackageId = "opencvcuda-debug.redist",
                    PackageVersion = "3.1.0",
                    CommitId = "e3f67b84-9c13-4f2f-ac80-12d08b6a23ce",
                    CommitTimestamp = DateTimeOffset.Parse("2018-12-10T07:25:55.4992861Z").AddDays(increment),
                },
            };

            var prefix = StorageUtility.GenerateDescendingId().ToString();
            var taskA = _latestPackageLeafStorageService.AddAsync(prefix, leaves);
            var taskB = _latestPackageLeafStorageService.AddAsync(prefix, leaves.Skip(1).ToList());

            await Task.WhenAll(taskA, taskB);
            */

            /*
            await _findPackageAssembliesDriver.ProcessLeafAsync(new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.08.23.17.47.42/cntk.deps.cudnn.2.6.0-rc0.dev20180823.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "CNTK.Deps.cuDNN",
                PackageVersion = "2.6.0-rc0.dev20180823",
            });

            await _findPackageAssembliesDriver.ProcessLeafAsync(new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.10.07.25.55/opencvcuda-debug.redist.3.1.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "opencvcuda-debug.redist",
                PackageVersion = "3.1.0",
            });
            */

            /*
            await _messageEnqueuer.EnqueueAsync(Enumerable
                .Range(0, 1000)
                .Select(b => new FindPackageAssetsCompactMessage
                {
                    Bucket = b,
                    Force = true,
                    DestinationContainer = FindPackageAssetsScanDriver.ContainerName,
                })
                .ToList());
            */

            /*
            await _driver.ProcessLeafAsync(new CatalogLeafScan
            {
                ScanParameters = _serializer.Serialize(new FindPackageAssetsParameters
                {
                    BucketCount = 1000, // Azure Data Explorer can only import up to 1000 blobs.
                }).AsString(),
                StorageSuffix = "foo",
                PackageId = "Microsoft.VisualStudio.Threading",
                PackageVersion = "15.5.24",
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.07.05.56.23/microsoft.visualstudio.threading.15.5.24.json",
                ParsedLeafType = CatalogLeafType.PackageDetails,
            });
            */

            // Success: {"n":"rrr","v":1,"d":{"i":"Newtonsoft.Json","v":"12.0.3","f":"netcoreapp1.1"}}
            // NU1202: { "n":"rrr","v":1,"d":{ "i":"Aspose.Words","v":"20.9.0","f":"netstandard1.6"} }
            // NU1213: { "n":"rrr","v":1,"d":{ "i":"Microsoft.AspNetCore.App.Runtime.linux-x64","v":"5.0.0-rc.1.20451.17","f":"netstandard1.5"} }
            // MSB3644: { "n":"rrr","v":1,"d":{ "i":"Newtonsoft.Json","v":"12.0.3","f":"net35"} }

            // await _catalogScanService.UpdateFindPackageAssetsAsync(max: null);
            // await _catalogScanService.UpdateFindPackageAssetsAsync(DateTimeOffset.Parse("2018-08-08T17:29:16.4488298Z"));
            // await EnqueueRunRealRestoreAsync();
            // await EnqueueRunRealRestoreCompactAsync();
            // await ReadErrorBlobsAsync();
            // await RetryRunRealRestoreAsync();
        }

        private async Task ReadErrorBlobsAsync()
        {
            var lines = File.ReadAllLines(@"C:\Users\jver\Desktop\error_blobs.txt");
            var baseUrl = "https://jverexplorepackages.blob.core.windows.net/runrealrestore/";
            using var httpClient = new HttpClient();

            var i = 0;
            foreach (var line in lines)
            {
                i++;
                if (i % 500 == 0)
                {
                    Console.WriteLine(i);
                }

                var url = $"{baseUrl}{line.Trim()}";
                var json = await httpClient.GetStringAsync(url);
                var errorResult = JsonConvert.DeserializeObject<RunRealRestoreErrorResult>(json);
                var restoreCommand = errorResult.CommandResults.FirstOrDefault(x => x.Arguments.FirstOrDefault() == "restore");
                var buildCommand = errorResult.CommandResults.FirstOrDefault(x => x.Arguments.FirstOrDefault() == "build");

                if (restoreCommand == null)
                {
                    Console.WriteLine($"{errorResult.Result.Id},{errorResult.Result.Version},{errorResult.Result.Framework}");
                    continue;
                }

                if (restoreCommand.Timeout
                    || restoreCommand.Output.Contains("There is not enough space on the disk.")
                    || (buildCommand != null && (buildCommand.Timeout || buildCommand.Output.Contains("There is not enough space on the disk."))))
                {
                    Console.WriteLine($"{errorResult.Result.Id},{errorResult.Result.Version},{errorResult.Result.Framework}");
                    continue;
                }
            }
        }

        private async Task EnqueueRunRealRestoreCompactAsync()
        {
            Console.WriteLine("Enqueueing messages...");
            var messages = Enumerable
                .Range(0, 1000)
                .Select(b => new RunRealRestoreCompactMessage { Bucket = b })
                .ToList();
            await _messageEnqueuer.EnqueueAsync(messages);
            Console.WriteLine("Done.");
        }

        private async Task RetryRunRealRestoreAsync()
        {
            var lines = File.ReadAllLines(@"C:\Users\jver\Desktop\IdVersionFramework.txt");
            var messages = new List<RunRealRestoreMessage>();
            foreach (var line in lines)
            {
                var pieces = line.Split('\t').Select(x => x.Trim()).ToList();
                messages.Add(new RunRealRestoreMessage { Id = pieces[0], Version = pieces[1], Framework = pieces[2] });
            }

            await _messageEnqueuer.EnqueueAsync(messages);
        }

        private async Task EnqueueRunRealRestoreAsync()
        {
            // var packageCount = 5001;

            // Source: https://docs.microsoft.com/en-us/dotnet/standard/frameworks
            var frameworks = new[]
            {
                ".NETCoreApp,Version=v1.0",
                ".NETCoreApp,Version=v1.1",
                ".NETCoreApp,Version=v2.0",
                ".NETCoreApp,Version=v2.1",
                ".NETCoreApp,Version=v2.2",
                ".NETCoreApp,Version=v3.0",
                ".NETCoreApp,Version=v3.1",
                // ".NETCoreApp,Version=v5.0", // Not yet supported in the .NET CLI installed on Azure Functions
                // ".NETFramework,Version=v1.1", // Does not appear significantly in the telemetry
                ".NETFramework,Version=v2.0",
                ".NETFramework,Version=v3.5",
                ".NETFramework,Version=v4.0",
                // ".NETFramework,Version=v4.0.3", // Does not appear significantly in the telemetry
                ".NETFramework,Version=v4.5",
                ".NETFramework,Version=v4.5.1",
                ".NETFramework,Version=v4.5.2",
                ".NETFramework,Version=v4.6",
                ".NETFramework,Version=v4.6.1",
                ".NETFramework,Version=v4.6.2",
                ".NETFramework,Version=v4.7",
                ".NETFramework,Version=v4.7.1",
                ".NETFramework,Version=v4.7.2",
                ".NETFramework,Version=v4.8",
                ".NETStandard,Version=v1.0",
                ".NETStandard,Version=v1.1",
                ".NETStandard,Version=v1.2",
                ".NETStandard,Version=v1.3",
                ".NETStandard,Version=v1.4",
                ".NETStandard,Version=v1.5",
                ".NETStandard,Version=v1.6",
                ".NETStandard,Version=v2.0",
                ".NETStandard,Version=v2.1",
            }
                .Select(x => NuGetFramework.Parse(x))
                .ToList();
            /*
            var source = "https://api.nuget.org/v3/index.json";
            var repository = Repository.Factory.GetCoreV3(source);
            var search = await repository.GetResourceAsync<PackageSearchResource>();
            var logger = NullLogger.Instance;
            var cancellationToken = CancellationToken.None;

            var packages = new HashSet<NuGetPackageIdentity>();
            var existingPackageIds = new HashSet<string>(File.ReadAllLines(@"C:\Users\jver\Desktop\PackageIds.txt"), StringComparer.OrdinalIgnoreCase);
            var skip = 0;
            var hasMoreResults = true;
            do
            {
                var take = Math.Min(1000, packageCount - packages.Count);

                Console.Write($"Searching for packages, skip = {skip}, take = {take}...");
                var results = await search.SearchAsync(
                    searchTerm: string.Empty,
                    new SearchFilter(includePrerelease: false),
                    skip: skip,
                    take: take,
                    log: logger,
                    cancellationToken: cancellationToken);
                Console.WriteLine(" done.");

                foreach (var result in results)
                {
                    if (!existingPackageIds.Contains(result.Identity.Id))
                    {
                        packages.Add(result.Identity);
                    }
                }

                var resultCount = results.Count();
                skip += resultCount;
                hasMoreResults = resultCount >= take;
            }
            while (packages.Count < packageCount && hasMoreResults);
            */
            var packages = new HashSet<NuGetPackageIdentity>();
            packages.Add(new NuGetPackageIdentity("Xam.Plugins.Android.ExoPlayer.MediaSession", NuGetVersion.Parse("2.11.8")));

            Console.WriteLine($"Found {packages.Count} packages.");

            Console.WriteLine("Enqueueing messages...");
            var messages = packages
                .SelectMany(p => frameworks.Select(f => new { Framework = f, Package = p }))
                .Select(m => new RunRealRestoreMessage
                {
                    Id = m.Package.Id,
                    Version = m.Package.Version.ToNormalizedString(),
                    Framework = m.Framework.GetShortFolderName(),
                })
                .ToList();
            await _messageEnqueuer.EnqueueAsync(messages);
            Console.WriteLine("Done.");
        }

        public bool IsInitializationRequired() => false;
        public bool IsDatabaseRequired() => false;
        public bool IsSingleton() => false;

    }
}
