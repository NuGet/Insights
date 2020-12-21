using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil;

namespace Knapcode.ExplorePackages.Worker.FindPackageAssemblies
{
    public class FindPackageAssembliesDriver : ICatalogLeafToCsvDriver<PackageAssembly>
    {
        private readonly CatalogClient _catalogClient;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<FindPackageAssembliesDriver> _logger;

        private static readonly HashSet<string> FileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".dll", ".exe"
        };

        public FindPackageAssembliesDriver(
            CatalogClient catalogClient,
            FlatContainerClient flatContainerClient,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<FindPackageAssembliesDriver> logger)
        {
            _catalogClient = catalogClient;
            _flatContainerClient = flatContainerClient;
            _options = options;
            _logger = logger;
        }

        public string ResultsContainerName => _options.Value.FindPackageAssembliesContainerName;
        public List<PackageAssembly> Prune(List<PackageAssembly> records) => PackageRecord.Prune(records);

        public async Task<List<PackageAssembly>> ProcessLeafAsync(CatalogLeafItem item)
        {
            Guid? scanId = null;
            DateTimeOffset? scanTimestamp = null;
            if (_options.Value.AppendResultUniqueIds)
            {
                scanId = Guid.NewGuid();
                scanTimestamp = DateTimeOffset.UtcNow;
            }

            if (item.Type == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);
                return new List<PackageAssembly> { new PackageAssembly(scanId, scanTimestamp, leaf) };
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);

                using var packageStream = await _flatContainerClient.DownloadPackageContentToFileAsync(item.PackageId, item.PackageVersion, CancellationToken.None);
                if (packageStream == null)
                {
                    // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted asset record.
                    return new List<PackageAssembly>();
                }

                using var zipArchive = new ZipArchive(packageStream);
                var entries = zipArchive
                    .Entries
                    .Where(x => FileExtensions.Contains(GetExtension(x.FullName)))
                    .ToList();

                if (!entries.Any())
                {
                    return new List<PackageAssembly> { new PackageAssembly(scanId, scanTimestamp, leaf, PackageAssemblyResultType.NoAssemblies) };
                }

                var assemblies = new List<PackageAssembly>();
                foreach (var entry in entries)
                {
                    var assembly = await AnalyzeAsync(scanId, scanTimestamp, leaf, entry);
                    assemblies.Add(assembly);
                }

                return assemblies;
            }
        }

        private async Task<PackageAssembly> AnalyzeAsync(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDetailsCatalogLeaf leaf, ZipArchiveEntry entry)
        {
            var assembly = new PackageAssembly(scanId, scanTimestamp, leaf, PackageAssemblyResultType.ValidAssembly)
            {
                Path = entry.FullName,
            };
            await AnalyzeAsync(assembly, entry);
            return assembly;
        }

        private async Task AnalyzeAsync(PackageAssembly assembly, ZipArchiveEntry entry)
        {
            using var entryStream = entry.Open();
            using var tempStream = await FileSystemUtility.CopyToTempStreamAsync(entryStream);

            try
            {
                using var module = ModuleDefinition.ReadModule(tempStream);
                var name = module.Assembly.Name;
                assembly.Name = name.Name;
                assembly.AssemblyVersion = name.Version.ToString();
                assembly.Culture = name.Culture;
                if (name.PublicKeyToken != null)
                {
                    assembly.PublicKeyToken = BitConverter.ToString(name.PublicKeyToken).Replace("-", string.Empty).ToLowerInvariant();
                }
            }
            catch (BadImageFormatException)
            {
                assembly.ResultType = PackageAssemblyResultType.NotManagedAssembly;
            }
        }

        private static string GetExtension(string path)
        {
            var dotIndex = path.LastIndexOf('.');
            if (dotIndex < 0)
            {
                return null;
            }

            return path.Substring(dotIndex);
        }
    }
}
