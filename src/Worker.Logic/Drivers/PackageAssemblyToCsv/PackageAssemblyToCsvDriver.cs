// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security;
using System.Security.Cryptography;
using Knapcode.MiniZip;

#nullable enable

namespace NuGet.Insights.Worker.PackageAssemblyToCsv
{
    public class PackageAssemblyToCsvDriver : ICatalogLeafToCsvDriver<PackageAssembly>, ICsvResultStorage<PackageAssembly>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageFileService _packageFileService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly FileDownloader _fileDownloader;
        private readonly TempStreamService _tempStreamService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<PackageAssemblyToCsvDriver> _logger;

        private static readonly HashSet<string> FileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".dll", ".exe" };

        public PackageAssemblyToCsvDriver(
            CatalogClient catalogClient,
            PackageFileService packageFileService,
            FlatContainerClient flatContainerClient,
            FileDownloader fileDownloader,
            TempStreamService tempStreamService,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<PackageAssemblyToCsvDriver> logger)
        {
            _catalogClient = catalogClient;
            _packageFileService = packageFileService;
            _flatContainerClient = flatContainerClient;
            _fileDownloader = fileDownloader;
            _tempStreamService = tempStreamService;
            _options = options;
            _logger = logger;
        }

        public string ResultContainerName => _options.Value.PackageAssemblyContainerName;
        public bool SingleMessagePerId => false;

        public async Task InitializeAsync()
        {
            await _packageFileService.InitializeAsync();
        }

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<IReadOnlyList<PackageAssembly>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                return MakeResults([new PackageAssembly(scanId, scanTimestamp, leaf)]);
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                var zipDirectory = await _packageFileService.GetZipDirectoryAsync(leafScan.ToPackageIdentityCommit());
                if (zipDirectory == null)
                {
                    return MakeEmptyResults();
                }

                if (!zipDirectory.Entries.Any(e => FileExtensions.Contains(Path.GetExtension(e.GetName()))))
                {
                    return MakeNoAssemblies(scanId, scanTimestamp, leaf);
                }

                var url = await _flatContainerClient.GetPackageContentUrlAsync(leafScan.PackageId, leafScan.PackageVersion);
                var result = await _fileDownloader.DownloadUrlToFileAsync(
                    url,
                    TempStreamWriter.GetTempFileNameFactory(
                        leafScan.PackageId,
                        leafScan.PackageVersion,
                        "assemblies",
                        ".nupkg"),
                    IncrementalHash.CreateNone,
                    CancellationToken.None);

                if (result is null)
                {
                    return MakeEmptyResults();
                }

                await using (result.Value.Body)
                {

                    if (result.Value.Body.Type == TempStreamResultType.SemaphoreNotAvailable)
                    {
                        return DriverResult.TryAgainLater<IReadOnlyList<PackageAssembly>>();
                    }

                    using var zipArchive = new ZipArchive(result.Value.Body.Stream);
                    var entries = zipArchive
                        .Entries
                        .Select((entry, i) => (SequenceNumber: i, Entry: entry))
                        .Where(x => FileExtensions.Contains(Path.GetExtension(x.Entry.FullName)))
                        .ToList();

                    if (!entries.Any())
                    {
                        return MakeNoAssemblies(scanId, scanTimestamp, leaf);
                    }

                    var assemblies = new List<PackageAssembly>();
                    foreach (var (sequenceNumber, entry) in entries)
                    {
                        var assemblyResult = await AnalyzeAsync(scanId, scanTimestamp, leaf, sequenceNumber, entry);
                        if (assemblyResult.Type == DriverResultType.TryAgainLater)
                        {
                            return DriverResult.TryAgainLater<IReadOnlyList<PackageAssembly>>();
                        }

                        assemblies.Add(assemblyResult.Value);
                    }

                    return MakeResults(assemblies);
                }
            }
        }

        private static DriverResult<IReadOnlyList<PackageAssembly>> MakeResults(IReadOnlyList<PackageAssembly> records)
        {
            return DriverResult.Success(records);
        }

        private static DriverResult<IReadOnlyList<PackageAssembly>> MakeEmptyResults()
        {
            // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted asset record.
            return MakeResults([]);
        }

        private static DriverResult<IReadOnlyList<PackageAssembly>> MakeNoAssemblies(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            return MakeResults([new PackageAssembly(scanId, scanTimestamp, leaf, PackageAssemblyResultType.NoAssemblies)]);
        }

        private async Task<DriverResult<PackageAssembly>> AnalyzeAsync(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, int sequenceNumber, ZipArchiveEntry entry)
        {
            var assembly = new PackageAssembly(scanId, scanTimestamp, leaf, PackageAssemblyResultType.ValidAssembly)
            {
                SequenceNumber = sequenceNumber,

                Path = entry.FullName,
                FileName = Path.GetFileName(entry.FullName),
                FileExtension = Path.GetExtension(entry.FullName),
                TopLevelFolder = PathUtility.GetTopLevelFolder(entry.FullName),
            };

            var result = await AnalyzeAsync(assembly, entry);
            if (result.Type == DriverResultType.TryAgainLater)
            {
                return DriverResult.TryAgainLater<PackageAssembly>();
            }

            return DriverResult.Success(assembly);
        }

        private async Task<DriverResult> AnalyzeAsync(PackageAssembly assembly, ZipArchiveEntry entry)
        {
            _logger.LogInformation("Analyzing ZIP entry {FullName} of length {Length} bytes.", entry.FullName, entry.Length);

            TempStreamResult? tempStreamResult = null;
            try
            {
                try
                {
                    tempStreamResult = await _tempStreamService.CopyToTempStreamAsync(
                        () => Task.FromResult(entry.Open()),
                        TempStreamWriter.GetTempFileNameFactory(
                            assembly.Id,
                            assembly.Version,
                            assembly.SequenceNumber?.ToString(CultureInfo.InvariantCulture),
                            assembly.FileExtension),
                        entry.Length,
                        IncrementalHash.CreateNone);
                }
                catch (InvalidDataException ex)
                {
                    assembly.ResultType = PackageAssemblyResultType.InvalidZipEntry;
                    _logger.LogInformation(ex, "Package {Id} {Version} has an invalid ZIP entry: {Path}", assembly.Id, assembly.Version, assembly.Path);
                    return DriverResult.Success();
                }

                if (tempStreamResult.Type == TempStreamResultType.SemaphoreNotAvailable)
                {
                    return DriverResult.TryAgainLater();
                }

                Analyze(assembly, tempStreamResult.Stream, _logger);

                return DriverResult.Success();
            }
            catch (BadImageFormatException ex)
            {
                assembly.ResultType = PackageAssemblyResultType.NotManagedAssembly;
                _logger.LogInformation(ex, "Package {Id} {Version} has an unmanaged assembly: {Path}", assembly.Id, assembly.Version, assembly.Path);
                return DriverResult.Success();
            }
            finally
            {
                if (tempStreamResult is not null)
                {
                    await tempStreamResult.DisposeAsync();
                }
            }
        }

        public static void Analyze(PackageAssembly assembly, Stream stream, ILogger logger)
        {
            assembly.FileLength = stream.Length;

            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                assembly.ResultType = PackageAssemblyResultType.NoManagedMetadata;
                return;
            }

            var metadataReader = peReader.GetMetadataReader();
            if (!metadataReader.IsAssembly)
            {
                assembly.ResultType = PackageAssemblyResultType.DoesNotContainAssembly;
                return;
            }

            var assemblyDefinition = metadataReader.GetAssemblyDefinition();

            assembly.AssemblyName = metadataReader.GetString(assemblyDefinition.Name);
            assembly.AssemblyVersion = assemblyDefinition.Version;
            assembly.Culture = metadataReader.GetString(assemblyDefinition.Culture);
            assembly.HashAlgorithm = assemblyDefinition.HashAlgorithm;
            assembly.EdgeCases = PackageAssemblyEdgeCases.None;

            SetPublicKeyInfo(assembly, metadataReader, assemblyDefinition);
            SetAssemblyAttributeInfo(assembly, metadataReader, logger);

            var assemblyName = GetAssemblyName(assembly, assemblyDefinition, logger);
            if (assemblyName != null)
            {
                SetPublicKeyTokenInfo(assembly, assemblyName, logger);
            }
        }

        private static AssemblyName? GetAssemblyName(PackageAssembly assembly, AssemblyDefinition assemblyDefinition, ILogger logger)
        {
            AssemblyName? assemblyName = null;
            try
            {
                assemblyName = assemblyDefinition.GetAssemblyName();
            }
            catch (CultureNotFoundException ex)
            {
                assembly.EdgeCases |= PackageAssemblyEdgeCases.Name_CultureNotFoundException;
                logger.LogInformation(ex, "Package {Id} {Version} has an invalid culture: {Path}", assembly.Id, assembly.Version, assembly.Path);
            }
            catch (FileLoadException ex)
            {
                assembly.EdgeCases |= PackageAssemblyEdgeCases.Name_FileLoadException;
                logger.LogInformation(ex, "Package {Id} {Version} has an AssemblyName that can't be loaded: {Path}", assembly.Id, assembly.Version, assembly.Path);
            }

            return assemblyName;
        }

        private static void SetPublicKeyInfo(PackageAssembly assembly, MetadataReader metadataReader, AssemblyDefinition assemblyDefinition)
        {
            if (assemblyDefinition.PublicKey.IsNil)
            {
                assembly.HasPublicKey = false;
                return;
            }

            assembly.HasPublicKey = true;
            var publicKey = metadataReader.GetBlobBytes(assemblyDefinition.PublicKey);
            assembly.PublicKeyLength = publicKey.Length;

            using var algorithm = SHA1.Create(); // SHA1 because that is what is used for the public key token
            assembly.PublicKeySHA1 = algorithm.ComputeHash(publicKey).ToBase64();
        }

        private static void SetPublicKeyTokenInfo(PackageAssembly assembly, AssemblyName assemblyName, ILogger logger)
        {
            byte[]? publicKeyTokenBytes = null;
            try
            {
                publicKeyTokenBytes = assemblyName.GetPublicKeyToken();
            }
            catch (SecurityException ex)
            {
                assembly.EdgeCases |= PackageAssemblyEdgeCases.PublicKeyToken_Security;
                logger.LogInformation(ex, "Package {Id} {Version} has an invalid public key. Path: {Path}", assembly.Id, assembly.Version, assembly.Path);
            }

            if (publicKeyTokenBytes != null && publicKeyTokenBytes.Length > 0)
            {
                assembly.PublicKeyToken = publicKeyTokenBytes.ToLowerHex();
            }
        }

        private static void SetAssemblyAttributeInfo(PackageAssembly assembly, MetadataReader metadataReader, ILogger logger)
        {
            var info = AssemblyAttributeReader.Read(metadataReader, assembly, logger);
            assembly.EdgeCases |= info.EdgeCases;
            assembly.CustomAttributesTotalCount = info.TotalCount;
            assembly.CustomAttributesTotalDataLength = info.TotalDataLength;
            assembly.CustomAttributesFailedDecode = KustoDynamicSerializer.Serialize(info.FailedDecode.OrderBy(x => x, StringComparer.Ordinal).ToList());
            assembly.CustomAttributes = KustoDynamicSerializer.Serialize(info.NameToParameters);
        }
    }
}
