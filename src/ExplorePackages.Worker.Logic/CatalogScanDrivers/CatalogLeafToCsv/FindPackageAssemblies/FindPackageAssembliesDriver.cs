using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.FindPackageAssemblies
{
    public class FindPackageAssembliesDriver : ICatalogLeafToCsvDriver<PackageAssembly>
    {
        private readonly CatalogClient _catalogClient;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly TempStreamService _tempStreamService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<FindPackageAssembliesDriver> _logger;

        private static readonly HashSet<string> FileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".dll", ".exe"
        };

        public FindPackageAssembliesDriver(
            CatalogClient catalogClient,
            FlatContainerClient flatContainerClient,
            TempStreamService tempStreamService,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<FindPackageAssembliesDriver> logger)
        {
            _catalogClient = catalogClient;
            _flatContainerClient = flatContainerClient;
            _tempStreamService = tempStreamService;
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

                using var packageStream = await _flatContainerClient.DownloadPackageContentToFileAsync(
                    item.PackageId,
                    item.PackageVersion,
                    CancellationToken.None);

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
            _logger.LogInformation("Analyzing ZIP entry {FullName} of length {Length} bytes.", entry.FullName, entry.Length);
            using var tempStream = await _tempStreamService.CopyToTempStreamAsync(() => entry.Open(), entry.Length);

            try
            {
                using var peReader = new PEReader(tempStream);
                if (!peReader.HasMetadata)
                {
                    assembly.ResultType = PackageAssemblyResultType.NotManagedAssembly;
                    return;
                }

                var metadataReader = peReader.GetMetadataReader();
                var assemblyDefinition = metadataReader.GetAssemblyDefinition();

                assembly.Name = metadataReader.GetString(assemblyDefinition.Name);
                assembly.AssemblyVersion = assemblyDefinition.Version;
                assembly.Culture = metadataReader.GetString(assemblyDefinition.Culture);
                assembly.HashAlgorithm = assemblyDefinition.HashAlgorithm.ToString();
                SetPublicKeyInfo(assembly, metadataReader, assemblyDefinition);
                var assemblyName = GetAssemblyName(assembly, assemblyDefinition);
                if (assemblyName != null)
                {
                    SetPublicKeyTokenInfo(assembly, assemblyName);
                }
            }
            catch (BadImageFormatException)
            {
                assembly.ResultType = PackageAssemblyResultType.NotManagedAssembly;
            }
        }

        private static AssemblyName GetAssemblyName(PackageAssembly assembly, AssemblyDefinition assemblyDefinition)
        {
            AssemblyName assemblyName = null;
            try
            {
                assemblyName = assemblyDefinition.GetAssemblyName();
                assembly.AssemblyNameHasCultureNotFoundException = false;
                assembly.AssemblyNameHasFileLoadException = false;
            }
            catch (CultureNotFoundException)
            {
                assembly.AssemblyNameHasCultureNotFoundException = true;
            }
            catch (FileLoadException)
            {
                assembly.AssemblyNameHasFileLoadException = true;
            }

            return assemblyName;
        }

        private static void SetPublicKeyInfo(PackageAssembly assembly, MetadataReader metadataReader, AssemblyDefinition assemblyDefinition)
        {
            if (assemblyDefinition.PublicKey.IsNil)
            {
                return;
            }

            assembly.HasPublicKey = true;
            var publicKey = metadataReader.GetBlobBytes(assemblyDefinition.PublicKey);
            assembly.PublicKeyLength = publicKey.Length;

            using (var sha256 = SHA256.Create())
            {
                assembly.PublicKeyHash = sha256.ComputeHash(publicKey).ToHex();
            }
        }

        private static void SetPublicKeyTokenInfo(PackageAssembly assembly, AssemblyName assemblyName)
        {
            byte[] publicKeyTokenBytes = null;
            try
            {
                publicKeyTokenBytes = assemblyName.GetPublicKeyToken();
                assembly.PublicKeyTokenHasSecurityException = false;
            }
            catch (SecurityException)
            {
                assembly.PublicKeyTokenHasSecurityException = true;
            }

            if (publicKeyTokenBytes != null)
            {
                assembly.PublicKeyToken = publicKeyTokenBytes.ToHex();
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
