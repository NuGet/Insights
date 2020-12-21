using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mono.Cecil;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker.FindPackageAssemblies
{
    public enum PackageAssemblyResultType
    {
        NoAssemblies,
        Error,
        NotManagedAssembly,
        ValidAssembly,
        Deleted,
    }

    public partial class PackageAssembly : PackageRecord, ICsvRecord
    {
        public PackageAssembly()
        {
        }

        public PackageAssembly(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageAssemblyResultType.Deleted;
        }

        public PackageAssembly(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDetailsCatalogLeaf leaf, PackageAssemblyResultType resultType)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = resultType;
        }

        public PackageAssemblyResultType ResultType { get; set; }

        public string Path { get; set; }
        public string Name { get; set; }
        public string AssemblyVersion { get; set; }
        public string Culture { get; set; }
        public string PublicKeyToken { get; set; }
    }

    public class FindPackageAssembliesDriver : ICatalogLeafToCsvDriver<PackageAssembly>
    {
        private readonly CatalogClient _catalogClient;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<FindPackageAssembliesDriver> _logger;

        private static readonly HashSet<string> FileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".dll", ".exe"
        };

        public FindPackageAssembliesDriver(
            CatalogClient catalogClient,
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainerClient,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<FindPackageAssembliesDriver> logger)
        {
            _catalogClient = catalogClient;
            _serviceIndexCache = serviceIndexCache;
            _flatContainerClient = flatContainerClient;
            _options = options;
            _logger = logger;
        }

        public string ResultsContainerName => throw new NotImplementedException();

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
                var assemblies = zipArchive
                    .Entries
                    .Where(x => FileExtensions.Contains(GetExtension(x.FullName)))
                    .Select(x => Analyze(scanId, scanTimestamp, leaf, x))
                    .ToList();

                if (!assemblies.Any())
                {
                    return new List<PackageAssembly> { new PackageAssembly(scanId, scanTimestamp, leaf, PackageAssemblyResultType.NoAssemblies) };
                }

                return assemblies;
            }
        }

        private PackageAssembly Analyze(Guid? scanId, DateTimeOffset? scanTimestamp, PackageDetailsCatalogLeaf leaf, ZipArchiveEntry entry)
        {
            var assembly = new PackageAssembly(scanId, scanTimestamp, leaf, PackageAssemblyResultType.ValidAssembly);
            Analyze(assembly, entry);
            return assembly;
        }

        private void Analyze(PackageAssembly assembly, ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            try
            {
                using var module = ModuleDefinition.ReadModule(stream);
                var name = module.Assembly.Name;
                assembly.Name = name.Name;
                assembly.AssemblyVersion = name.Version.ToString(fieldCount: 4);
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

        public List<PackageAssembly> Prune(List<PackageAssembly> records)
        {
            throw new NotImplementedException();
        }
    }
}
