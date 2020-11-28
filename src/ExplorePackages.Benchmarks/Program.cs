using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Knapcode.ExplorePackages.Worker;
using Knapcode.ExplorePackages.Worker.FindPackageAssets;
using Microsoft.Extensions.DependencyInjection;

namespace Knapcode.ExplorePackages
{
    public class TheAppendResultStorageServiceClass
    {
        public class TheCompactAsyncMethod : IDisposable
        {
            private readonly ServiceProvider _serviceProvider;
            private readonly AppendResultStorageService _appendResultStorageService;

            public TheCompactAsyncMethod()
            {
                var serviceCollection = new ServiceCollection();

                serviceCollection.AddExplorePackages("Knapcode.ExplorePackages.Benchmarks");
                serviceCollection.AddExplorePackagesWorker();

                serviceCollection.Configure<ExplorePackagesWorkerSettings>(x =>
                {
                    x.AppendResultStorageMode = AppendResultStorageMode.AppendBlob;
                });

                _serviceProvider = serviceCollection.BuildServiceProvider();
                _appendResultStorageService = _serviceProvider.GetRequiredService<AppendResultStorageService>();
            }

            public void Dispose() => _serviceProvider.Dispose();

            [Benchmark]
            public async Task ExecuteAsync()
            {
                await _appendResultStorageService.CompactAsync<PackageAsset>(
                   "findpackageassets",
                   "findpackageassets",
                   0,
                   force: true,
                   mergeExisting: true,
                   PruneAssets);
            }

            private static List<PackageAsset> PruneAssets(List<PackageAsset> allAssets)
            {
                return allAssets
                    .GroupBy(x => new { Id = x.Id.ToLowerInvariant(), Version = x.Version.ToLowerInvariant() }) // Group by unique package version
                    .Select(g => g
                        .GroupBy(x => x.ScanId) // Group package version assets by scan
                        .OrderByDescending(x => x.First().Created) // Ignore all but the most recent scan of the most recent version of the package
                        .OrderByDescending(x => x.First().ScanTimestamp)
                        .First())
                    .SelectMany(g => g)
                    .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                    .Distinct()
                    .ToList();
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            IConfig config = null;
#if DEBUG
            config = new DebugInProcessConfig();
#endif
            BenchmarkRunner.Run<TheAppendResultStorageServiceClass.TheCompactAsyncMethod>(config);
        }
    }
}
