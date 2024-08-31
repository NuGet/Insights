// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Knapcode.MiniZip;
using static NuGet.Insights.StorageUtility;

namespace NuGet.Insights.Worker
{
    public abstract class ZipArchiveMetadataToCsvDriver<TArchive, TEntry> :
        ICatalogLeafToCsvDriver<TArchive, TEntry>
        where TArchive : ArchiveRecord, IAggregatedCsvRecord<TArchive>
        where TEntry : ArchiveEntry, IAggregatedCsvRecord<TEntry>
    {
        private readonly CatalogClient _catalogClient;
        private readonly PackageSpecificHashService _hashService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public ZipArchiveMetadataToCsvDriver(
            CatalogClient catalogClient,
            PackageSpecificHashService hashService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _catalogClient = catalogClient;
            _hashService = hashService;
            _options = options;
        }

        public bool SingleMessagePerId => false;
        protected abstract bool NotFoundIsDeleted { get; }
        protected abstract ArtifactFileType FileType { get; }

        public async Task InitializeAsync()
        {
            await _hashService.InitializeAsync();
            await InternalInitializeAsync();
        }

        protected abstract TArchive NewArchiveDeleteRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf);
        protected abstract TEntry NewEntryDeleteRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf);
        protected abstract TArchive NewArchiveDetailsRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf);
        protected abstract TEntry NewEntryDetailsRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf);
        protected abstract Task InternalInitializeAsync();
        protected abstract Task<(ZipDirectory directory, long length, ILookup<string, string> headers)> GetZipDirectoryAndLengthAsync(IPackageIdentityCommit leafItem);

        public Task DestroyAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<DriverResult<CsvRecordSets<TArchive, TEntry>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            (var archive, var entries) = await ProcessLeafInternalAsync(leafScan);
            return DriverResult.Success(new CsvRecordSets<TArchive, TEntry>(
                archive != null ? [archive] : [],
                entries ?? []));
        }

        private async Task<(TArchive, IReadOnlyList<TEntry>)> ProcessLeafInternalAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);
                return (
                    NewArchiveDeleteRecord(scanId, scanTimestamp, leaf),
                    [NewEntryDeleteRecord(scanId, scanTimestamp, leaf)]
                );
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                var hashes = await _hashService.GetHashesAsync(leafScan.ToPackageIdentityCommit(), requireFresh: false);
                if (hashes is null)
                {
                    if (NotFoundIsDeleted)
                    {
                        // Ignore packages where the hashes are missing. A subsequent scan will produce a deleted record.
                        return (null, null);
                    }
                    else
                    {
                        return MakeDoesNotExist(scanId, scanTimestamp, leaf);
                    }
                }

                (var zipDirectory, var size, var headers) = await GetZipDirectoryAndLengthAsync(leafScan.ToPackageIdentityCommit());
                if (zipDirectory is null)
                {
                    if (NotFoundIsDeleted)
                    {
                        // Ignore packages where the ZIP directory is missing. A subsequent scan will produce a deleted record.
                        return (null, null);
                    }
                    else
                    {
                        return MakeDoesNotExist(scanId, scanTimestamp, leaf);
                    }
                }

                // Necessary because of https://github.com/neuecc/MessagePack-CSharp/issues/1431
                var headerMD5 = !_options.Value.SkipContentMD5HeaderInCsv && headers.Contains(MD5Header) ? headers[MD5Header].SingleOrDefault() : null;
                var headerSHA512 = headers.Contains(SHA512Header) ? headers[SHA512Header].SingleOrDefault() : null;

                var archive = NewArchiveDetailsRecord(scanId, scanTimestamp, leaf);

                archive.Size = size;
                archive.OffsetAfterEndOfCentralDirectory = zipDirectory.OffsetAfterEndOfCentralDirectory;
                archive.CentralDirectorySize = zipDirectory.CentralDirectorySize;
                archive.OffsetOfCentralDirectory = zipDirectory.OffsetOfCentralDirectory;
                archive.EntryCount = zipDirectory.Entries.Count;
                archive.Comment = zipDirectory.GetComment();

                archive.HeaderMD5 = headerMD5;
                archive.HeaderSHA512 = headerSHA512;

                archive.MD5 = hashes.MD5.ToBase64();
                archive.SHA1 = hashes.SHA1.ToBase64();
                archive.SHA256 = hashes.SHA256.ToBase64();
                archive.SHA512 = hashes.SHA512.ToBase64();

                var entries = new List<TEntry>();

                foreach (var entryInfo in zipDirectory.Entries)
                {
                    var path = entryInfo.GetName();

                    var entry = NewEntryDetailsRecord(scanId, scanTimestamp, leaf);

                    entry.SequenceNumber = entries.Count;

                    entry.Path = path;
                    entry.FileName = Path.GetFileName(path);
                    entry.FileExtension = Path.GetExtension(path);
                    entry.TopLevelFolder = PathUtility.GetTopLevelFolder(path);

                    entry.Flags = entryInfo.Flags;
                    entry.CompressionMethod = entryInfo.CompressionMethod;
                    entry.LastModified = new DateTimeOffset(entryInfo.GetLastModified(), TimeSpan.Zero);
                    entry.Crc32 = entryInfo.Crc32;
                    entry.CompressedSize = entryInfo.CompressedSize;
                    entry.UncompressedSize = entryInfo.UncompressedSize;
                    entry.LocalHeaderOffset = entryInfo.LocalHeaderOffset;
                    entry.Comment = entryInfo.GetComment();

                    entries.Add(entry);
                }

                // NuGet packages must contain contain at least a .nuspec file.
                if (!entries.Any())
                {
                    throw new InvalidOperationException($"{FileType} ZIP archive has no entries for catalog leaf item {leafScan.Url}.");
                }

                return (archive, entries);
            }
        }

        private (TArchive, IReadOnlyList<TEntry>) MakeDoesNotExist(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
        {
            var archive = NewArchiveDetailsRecord(scanId, scanTimestamp, leaf);
            archive.ResultType = ArchiveResultType.DoesNotExist;

            var entry = NewEntryDetailsRecord(scanId, scanTimestamp, leaf);
            entry.ResultType = ArchiveResultType.DoesNotExist;

            return (archive, [entry]);
        }
    }
}
