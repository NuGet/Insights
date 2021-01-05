using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker;
using Knapcode.ExplorePackages.Worker.FindLatestPackageLeaf;
using Knapcode.ExplorePackages.Worker.RunRealRestore;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetPackageIdentity = NuGet.Packaging.Core.PackageIdentity;

namespace Knapcode.ExplorePackages.Tool
{
    public class SandboxCommand : ICommand
    {
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IRawMessageEnqueuer _rawMessageEnqueuer;
        private readonly TableScanService<LatestPackageLeaf> _tableScanService;

        public SandboxCommand(
            MessageEnqueuer messageEnqueuer,
            TaskStateStorageService taskStateStorageService,
            ServiceClientFactory serviceClientFactory,
            IRawMessageEnqueuer rawMessageEnqueuer,
            TableScanService<LatestPackageLeaf> tableScanService)
        {
            _messageEnqueuer = messageEnqueuer;
            _taskStateStorageService = taskStateStorageService;
            _serviceClientFactory = serviceClientFactory;
            _rawMessageEnqueuer = rawMessageEnqueuer;
            _tableScanService = tableScanService;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Yield();

            await RunTestAsync(1, 1);

            for (var i = 1; i <= 5; i++)
            {
                for (var j = 1; j <= 5; j++)
                {
                    await RunTestAsync(i, j);
                }
            }
        }

        private async Task RunTestAsync(int i, int j)
        {
            var taskStateKey = new TaskStateKey("copy", "copy", "copy");
            await _taskStateStorageService.InitializeAsync(taskStateKey.StorageSuffix);

            await _taskStateStorageService.GetOrAddAsync(taskStateKey);

            var table = _serviceClientFactory.GetStorageAccount().CreateCloudTableClient().GetTableReference("latestpackageleavesps");
            await table.DeleteIfExistsAsync();
            await table.CreateIfNotExistsAsync(retry: true);

            var sw = Stopwatch.StartNew();
            await _tableScanService.StartTableCopyAsync(
                taskStateKey,
                "latestpackageleaves",
                table.Name,
                string.Empty,
                TableScanStrategy.PrefixScan,
                StorageUtility.MaxTakeCount,
                segmentsPerFirstPrefix: i,
                segmentsPerSubsequentPrefix: j);

            int countLowerBound = -1;
            do
            {
                if (countLowerBound > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                countLowerBound = await _taskStateStorageService.GetCountLowerBoundAsync(taskStateKey.StorageSuffix, taskStateKey.PartitionKey);
                Console.WriteLine($"[{sw.Elapsed}, {i}, {j}] count lower bound: {countLowerBound}, queue message count: {await _rawMessageEnqueuer.GetApproximateMessageCountAsync()}");
            }
            while (countLowerBound > 0);

            Console.WriteLine($"[{sw.Elapsed}, {i}, {j}] complete");
        }

        private async Task WorkOnRealRestoreAsync()
        {
            await Task.Yield();

            // Success: {"n":"rrr","v":1,"d":{"i":"Newtonsoft.Json","v":"12.0.3","f":"netcoreapp1.1"}}
            // NU1202: { "n":"rrr","v":1,"d":{ "i":"Aspose.Words","v":"20.9.0","f":"netstandard1.6"} }
            // NU1213: { "n":"rrr","v":1,"d":{ "i":"Microsoft.AspNetCore.App.Runtime.linux-x64","v":"5.0.0-rc.1.20451.17","f":"netstandard1.5"} }
            // MSB3644: { "n":"rrr","v":1,"d":{ "i":"Newtonsoft.Json","v":"12.0.3","f":"net35"} }

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

            var packages = new HashSet<NuGetPackageIdentity>();
            // await AddTopPackagesAsync(packages);
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

        private static async Task AddTopPackagesAsync(HashSet<NuGetPackageIdentity> packages)
        {
            var packageCount = 5001;

            var source = "https://api.nuget.org/v3/index.json";
            var repository = Repository.Factory.GetCoreV3(source);
            var search = await repository.GetResourceAsync<PackageSearchResource>();
            var logger = NullLogger.Instance;
            var cancellationToken = CancellationToken.None;

            var existingPackageIds = new HashSet<string>(File.ReadAllLines(@"C:\Users\jver\Desktop\PackageIds.txt"), StringComparer.OrdinalIgnoreCase);
            var skip = 0;
            bool hasMoreResults;
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
        }

        public bool IsInitializationRequired() => false;
        public bool IsDatabaseRequired() => false;
        public bool IsSingleton() => false;

    }
}
