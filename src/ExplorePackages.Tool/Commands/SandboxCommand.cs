using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Logic.Worker;
using Knapcode.ExplorePackages.Logic.Worker.BlobStorage;
using Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets;
using Knapcode.ExplorePackages.Logic.Worker.RunRealRestore;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages.Tool
{
    public class SandboxCommand : ICommand
    {
        private readonly CatalogScanService _catalogScanService;
        private readonly IWorkerQueueFactory _workerQueueFactory;
        private readonly CursorStorageService _cursorStorageService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly LatestPackageLeafStorageService _latestPackageLeafStorageService;
        private readonly AppendResultStorageService _findPackageAssetsStorageService;
        private readonly IMessageProcessor<RunRealRestoreMessage> _messageProcessor;
        private readonly MessageEnqueuer _messageEnqueuer;

        public SandboxCommand(
            CatalogScanService catalogScanService,
            IWorkerQueueFactory workerQueueFactory,
            CursorStorageService cursorStorageService,
            CatalogScanStorageService catalogScanStorageService,
            LatestPackageLeafStorageService latestPackageLeafStorageService,
            AppendResultStorageService findPackageAssetsStorageService,
            IMessageProcessor<RunRealRestoreMessage> messageProcessor,
            MessageEnqueuer messageEnqueuer)
        {
            _catalogScanService = catalogScanService;
            _workerQueueFactory = workerQueueFactory;
            _cursorStorageService = cursorStorageService;
            _catalogScanStorageService = catalogScanStorageService;
            _latestPackageLeafStorageService = latestPackageLeafStorageService;
            _findPackageAssetsStorageService = findPackageAssetsStorageService;
            _messageProcessor = messageProcessor;
            _messageEnqueuer = messageEnqueuer;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _workerQueueFactory.GetQueue().CreateIfNotExistsAsync(retry: true);
            await _cursorStorageService.InitializeAsync();
            await _catalogScanStorageService.InitializeAsync();
            await _latestPackageLeafStorageService.InitializeAsync();
            await _findPackageAssetsStorageService.InitializeAsync(FindPackageAssetsConstants.ContainerName);
            await _findPackageAssetsStorageService.InitializeAsync(RunRealRestoreConstants.ContainerName);

            // Success: {"n":"rrr","v":1,"d":{"i":"Newtonsoft.Json","v":"12.0.3","f":"netcoreapp1.1"}}
            // NU1202: { "n":"rrr","v":1,"d":{ "i":"Aspose.Words","v":"20.9.0","f":"netstandard1.6"} }
            // NU1213: { "n":"rrr","v":1,"d":{ "i":"Microsoft.AspNetCore.App.Runtime.linux-x64","v":"5.0.0-rc.1.20451.17","f":"netstandard1.5"} }
            // MSB3644: { "n":"rrr","v":1,"d":{ "i":"Newtonsoft.Json","v":"12.0.3","f":"net35"} }

            // await _catalogScanService.UpdateGetPackageAssets();
            await EnqueueRunRealRestoreAsync();
            // await EnqueueRunRealRestoreCompactAsync();
            // await ReadErrorBlobsAsync();
        }

        private async Task ReadErrorBlobsAsync()
        {
            var lines = File.ReadAllLines(@"C:\Users\jver\Desktop\error_blobs.txt");
            var baseUrl = "https://jverexplorepackages.blob.core.windows.net/runrealrestore/";
            using var httpClient = new HttpClient();

            foreach (var line in lines)
            {
                var url = $"{baseUrl}{line.Trim()}";
                Console.WriteLine(url);
                var json = await httpClient.GetStringAsync(url);
                var errorResult = JsonConvert.DeserializeObject<RunRealRestoreErrorResult>(json);
                var restoreCommand = errorResult.CommandResults.FirstOrDefault(x => x.Arguments.Contains("restore"));
                if (restoreCommand == null)
                {
                    Console.WriteLine("  No restore found!");
                    continue;
                }

                var matches = Regex.Matches(restoreCommand.Output, "(NU\\d+)");
                var errors = matches
                    .GroupBy(x => x.Groups[1].Value)
                    .ToDictionary(x => x.Key, x => x.Count())
                    .OrderByDescending(x => x.Value);
                
                foreach (var pair in errors)
                {
                    Console.WriteLine($"  {pair.Key}: {pair.Value}");
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

        private async Task EnqueueRunRealRestoreAsync()
        {
            var frameworkCount = 1000;
            var packageCount = 1;

            var frameworks = new[]
            {
                ".NETCoreApp,Version=v1.1",
                ".NETCoreApp,Version=v2.0",
                ".NETCoreApp,Version=v2.1",
                ".NETCoreApp,Version=v2.2",
                ".NETCoreApp,Version=v3.0",
                ".NETCoreApp,Version=v3.1",
                ".NETCoreApp,Version=v5.0",
                ".NETFramework,Version=v2.0",
                ".NETFramework,Version=v3.5",
                ".NETFramework,Version=v4.0",
                ".NETFramework,Version=v4.5.1",
                ".NETFramework,Version=v4.5.2",
                ".NETFramework,Version=v4.5",
                ".NETFramework,Version=v4.6.1",
                ".NETFramework,Version=v4.6.2",
                ".NETFramework,Version=v4.6",
                ".NETFramework,Version=v4.7.1",
                ".NETFramework,Version=v4.7.2",
                ".NETFramework,Version=v4.7",
                ".NETFramework,Version=v4.8",
                ".NETStandard,Version=v1.0",
                ".NETStandard,Version=v1.1",
                ".NETStandard,Version=v1.3",
                ".NETStandard,Version=v1.4",
                ".NETStandard,Version=v1.5",
                ".NETStandard,Version=v1.6",
                ".NETStandard,Version=v2.0",
            }
                .Select(x => NuGetFramework.Parse(x))
                .ToList();

            var source = "https://api.nuget.org/v3/index.json";
            var repository = Repository.Factory.GetCoreV3(source);
            var search = await repository.GetResourceAsync<PackageSearchResource>();
            var logger = NullLogger.Instance;
            var cancellationToken = CancellationToken.None;

            Console.Write($"Searching for top {packageCount} packages...");
            var results = await search.SearchAsync(
                searchTerm: string.Empty,
                new SearchFilter(includePrerelease: true),
                skip: 0,
                take: packageCount,
                log: logger,
                cancellationToken: cancellationToken);
            Console.WriteLine(" done.");

            Console.WriteLine("Enqueueing messages...");
            var messages = results
                .SelectMany(p => frameworks
                    .Take(frameworkCount)
                    .Select(f => new { Framework = f, Package = p.Identity }))
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
