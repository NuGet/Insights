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
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            // await _catalogScanService.InitializeAsync();

            try
            {
                await _findPackageAssembliesDriver.ProcessLeafAsync(new CatalogLeafItem
                {
                    Url = "https://api.nuget.org/v3/catalog0/data/2018.10.11.03.47.42/sharepointpnpcoreonline.2.21.1712.json",
                    Type = CatalogLeafType.PackageDetails,
                    CommitId = "1665ef2d-87d6-42ef-8fc0-8709bbb1f3f2",
                    CommitTimestamp = DateTimeOffset.Parse("2018-10-11T03:47:42.189Z"),
                    PackageId = "SharePointPnPCoreOnline",
                    PackageVersion = "2.21.1712",
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.WriteLine();

            try
            {
                await _findPackageAssembliesDriver.ProcessLeafAsync(new CatalogLeafItem
                {
                    Url = "https://api.nuget.org/v3/catalog0/data/2018.12.18.08.44.52/enyutrynuget.1.0.0.json",
                    Type = CatalogLeafType.PackageDetails,
                    CommitId = "2e0ef1b6-69d1-4822-abdb-4d45c2d57c1b",
                    CommitTimestamp = DateTimeOffset.Parse("2018-12-18T08:44:52.180Z"),
                    PackageId = "EnyuTryNuget",
                    PackageVersion = "1.0.0",
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.WriteLine();

            try
            {
                await _findPackageAssembliesDriver.ProcessLeafAsync(new CatalogLeafItem
                {
                    Url = "https://api.nuget.org/v3/catalog0/data/2018.12.11.04.41.19/getaddress.azuretablestorage.1.0.0.json",
                    Type = CatalogLeafType.PackageDetails,
                    CommitId = "646bc4bc-4b67-45ba-aceb-76488ed487eb",
                    CommitTimestamp = DateTimeOffset.Parse("2018-12-11T04:41:19.791Z"),
                    PackageId = "getAddress.AzureTableStorage",
                    PackageVersion = "1.0.0",
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

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
            // await _catalogScanService.RequeueAsync("08585954065972383328-9a556c36a70a48078031b866de666a4b");
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
