// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
using Newtonsoft.Json;

namespace NuGet.Insights.Worker.PackageAssemblyToCsv
{
    public class PackageAssemblyToCsvDriver : ICatalogLeafToCsvDriver<PackageAssembly>, ICsvResultStorage<PackageAssembly>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageHashService _packageHashService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly TempStreamService _tempStreamService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<PackageAssemblyToCsvDriver> _logger;

        private static readonly HashSet<string> FileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".dll", ".exe" };

        public PackageAssemblyToCsvDriver(
            CatalogClient catalogClient,
            PackageHashService packageHashService,
            FlatContainerClient flatContainerClient,
            TempStreamService tempStreamService,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<PackageAssemblyToCsvDriver> logger)
        {
            _catalogClient = catalogClient;
            _packageHashService = packageHashService;
            _flatContainerClient = flatContainerClient;
            _tempStreamService = tempStreamService;
            _options = options;
            _logger = logger;
        }

        public string ResultContainerName => _options.Value.PackageAssemblyContainerName;
        public bool SingleMessagePerId => false;

        public List<PackageAssembly> Prune(List<PackageAssembly> records)
        {
            return PackageRecord.Prune(records);
        }

        public async Task InitializeAsync()
        {
            await _packageHashService.InitializeAsync();
        }

        public async Task<DriverResult<CsvRecordSet<PackageAssembly>>> ProcessLeafAsync(ICatalogLeafItem item, int attemptCount)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (item.Type == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);

                // We must clear the data related to deleted packages.
                await _packageHashService.SetHashesAsync(item, hashes: null);

                return MakeResults(item, new List<PackageAssembly> { new PackageAssembly(scanId, scanTimestamp, leaf) });
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(item.Type, item.Url);

                using var result = await _flatContainerClient.DownloadPackageContentToFileAsync(
                    item.PackageId,
                    item.PackageVersion,
                    CancellationToken.None);

                if (result == null)
                {
                    // We must clear the data related to deleted packages.
                    await _packageHashService.SetHashesAsync(item, hashes: null);

                    return MakeEmptyResults(item);
                }

                if (result.Type == TempStreamResultType.SemaphoreNotAvailable)
                {
                    return DriverResult.TryAgainLater<CsvRecordSet<PackageAssembly>>();
                }

                // We have downloaded the full .nupkg here so we can capture the calculated hashes.
                await _packageHashService.SetHashesAsync(item, result.Hash);

                using var zipArchive = new ZipArchive(result.Stream);
                var entries = zipArchive
                    .Entries
                    .Where(x => FileExtensions.Contains(Path.GetExtension(x.FullName)))
                    .ToList();

                if (!entries.Any())
                {
                    return MakeNoAssemblies(scanId, scanTimestamp, leaf);
                }

                var assemblies = new List<PackageAssembly>();
                foreach (var entry in entries)
                {
                    var assemblyResult = await AnalyzeAsync(scanId, scanTimestamp, leaf, entry);
                    if (assemblyResult.Type == DriverResultType.TryAgainLater)
                    {
                        return DriverResult.TryAgainLater<CsvRecordSet<PackageAssembly>>();
                    }

                    assemblies.Add(assemblyResult.Value);
                }

                return MakeResults(item, assemblies);
            }
        }

        private static DriverResult<CsvRecordSet<PackageAssembly>> MakeResults(ICatalogLeafItem item, List<PackageAssembly> records)
        {
            return DriverResult.Success(new CsvRecordSet<PackageAssembly>(PackageRecord.GetBucketKey(item), records));
        }

        private static DriverResult<CsvRecordSet<PackageAssembly>> MakeEmptyResults(ICatalogLeafItem item)
        {
            // Ignore packages where the .nupkg is missing. A subsequent scan will produce a deleted asset record.
            return MakeResults(item, new List<PackageAssembly>());
        }

        private static DriverResult<CsvRecordSet<PackageAssembly>> MakeNoAssemblies(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            return MakeResults(leaf, new List<PackageAssembly> { new PackageAssembly(scanId, scanTimestamp, leaf, PackageAssemblyResultType.NoAssemblies) });
        }

        private async Task<DriverResult<PackageAssembly>> AnalyzeAsync(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf, ZipArchiveEntry entry)
        {
            var assembly = new PackageAssembly(scanId, scanTimestamp, leaf, PackageAssemblyResultType.ValidAssembly)
            {
                Path = entry.FullName,
                FileName = Path.GetFileName(entry.FullName),
                FileExtension = Path.GetExtension(entry.FullName),
                TopLevelFolder = PathUtility.GetTopLevelFolder(entry.FullName),

                CompressedLength = entry.CompressedLength,
                EntryUncompressedLength = entry.Length,
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

            TempStreamResult tempStreamResult = null;
            try
            {
                try
                {
                    tempStreamResult = await _tempStreamService.CopyToTempStreamAsync(() => entry.Open(), entry.Length, IncrementalHash.CreateSHA256);
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

                assembly.ActualUncompressedLength = tempStreamResult.Stream.Length;
                assembly.FileSHA256 = tempStreamResult.Hash.SHA256.ToBase64();

                using var peReader = new PEReader(tempStreamResult.Stream);
                if (!peReader.HasMetadata)
                {
                    assembly.ResultType = PackageAssemblyResultType.NoManagedMetadata;
                    return DriverResult.Success();
                }

                var metadataReader = peReader.GetMetadataReader();
                if (!metadataReader.IsAssembly)
                {
                    assembly.ResultType = PackageAssemblyResultType.DoesNotContainAssembly;
                    return DriverResult.Success();
                }

                var assemblyDefinition = metadataReader.GetAssemblyDefinition();

                assembly.AssemblyName = metadataReader.GetString(assemblyDefinition.Name);
                assembly.AssemblyVersion = assemblyDefinition.Version;
                assembly.Culture = metadataReader.GetString(assemblyDefinition.Culture);
                assembly.HashAlgorithm = assemblyDefinition.HashAlgorithm;
                assembly.EdgeCases = PackageAssemblyEdgeCases.None;
                
                SetPublicKeyInfo(assembly, metadataReader, assemblyDefinition);
                SetAssemblyAttributeInfo(assembly, metadataReader);

                var assemblyName = GetAssemblyName(assembly, assemblyDefinition);
                if (assemblyName != null)
                {
                    SetPublicKeyTokenInfo(assembly, assemblyName);
                }

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
                tempStreamResult?.Dispose();
            }
        }

        private AssemblyName GetAssemblyName(PackageAssembly assembly, AssemblyDefinition assemblyDefinition)
        {
            AssemblyName assemblyName = null;
            try
            {
                assemblyName = assemblyDefinition.GetAssemblyName();
            }
            catch (CultureNotFoundException ex)
            {
                assembly.EdgeCases |= PackageAssemblyEdgeCases.Name_CultureNotFoundException;
                _logger.LogInformation(ex, "Package {Id} {Version} has an invalid culture: {Path}", assembly.Id, assembly.Version, assembly.Path);
            }
            catch (FileLoadException ex)
            {
                assembly.EdgeCases |= PackageAssemblyEdgeCases.Name_FileLoadException;
                _logger.LogInformation(ex, "Package {Id} {Version} has an AssemblyName that can't be loaded: {Path}", assembly.Id, assembly.Version, assembly.Path);
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

        private void SetPublicKeyTokenInfo(PackageAssembly assembly, AssemblyName assemblyName)
        {
            byte[] publicKeyTokenBytes = null;
            try
            {
                publicKeyTokenBytes = assemblyName.GetPublicKeyToken();
            }
            catch (SecurityException ex)
            {
                assembly.EdgeCases |= PackageAssemblyEdgeCases.PublicKeyToken_Security;
                _logger.LogInformation(ex, "Package {Id} {Version} has an invalid public key. Path: {Path}", assembly.Id, assembly.Version, assembly.Path);
            }

            if (publicKeyTokenBytes != null)
            {
                assembly.PublicKeyToken = publicKeyTokenBytes.ToHex();
            }
        }

        private void SetAssemblyAttributeInfo(PackageAssembly assembly, MetadataReader metadataReader)
        {
            var info = AssemblyAttributeReader.Read(metadataReader, assembly, _logger);
            assembly.EdgeCases |= info.EdgeCases;
            assembly.CustomAttributesTotalCount = info.TotalCount;
            assembly.CustomAttributesTotalDataLength = info.TotalDataLength;
            assembly.CustomAttributesFailedDecode = JsonConvert.SerializeObject(info.FailedDecode);
            assembly.CustomAttributes = JsonConvert.SerializeObject(info.NameToParameters);
        }

        public Task<ICatalogLeafItem> MakeReprocessItemOrNullAsync(PackageAssembly record)
        {
            throw new NotImplementedException();
        }
    }
}
