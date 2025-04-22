// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Buffers;
using System.IO.Compression;
using Knapcode.MiniZip;

#nullable enable

namespace NuGet.Insights.Worker
{
    public abstract class FullZipArchiveEntryToCsvDriver<T> : ICatalogLeafToCsvDriver<T>, ICsvResultStorage<T>
        where T : FileRecord, IAggregatedCsvRecord<T>
    {
        private readonly CatalogClient _catalogClient;
        private readonly FileDownloader _fileDownloader;
        private readonly PackageSpecificHashService _hashService;
        private readonly ILogger _logger;

        public FullZipArchiveEntryToCsvDriver(
            CatalogClient catalogClient,
            FileDownloader fileDownloader,
            PackageSpecificHashService hashService,
            ILogger logger)
        {
            _catalogClient = catalogClient;
            _fileDownloader = fileDownloader;
            _hashService = hashService;
            _logger = logger;
        }

        public bool SingleMessagePerId => false;
        public abstract string ResultContainerName { get; }
        protected abstract bool NotFoundIsDeleted { get; }
        protected abstract ArtifactFileType FileType { get; }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                _hashService.InitializeAsync(),
                InternalInitializeAsync());
        }

        protected abstract T NewDeleteRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf);
        protected abstract T NewDetailsRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf);
        protected abstract Task<string> GetZipUrlAsync(CatalogLeafScan leafScan);
        protected abstract Task<ZipDirectory?> GetZipDirectoryAsync(IPackageIdentityCommit leafItem);
        protected abstract Task InternalInitializeAsync();

        public async Task<DriverResult<IReadOnlyList<T>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                // We must clear the data related to deleted ZIP archives.
                await _hashService.SetHashesAsync(leafScan.ToPackageIdentityCommit(), headers: null, archiveHashes: null, entryHashes: null);

                return MakeResults([NewDeleteRecord(scanId, scanTimestamp, leaf)]);
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                var zipDirectory = await GetZipDirectoryAsync(leafScan.ToPackageIdentityCommit());
                if (zipDirectory is null)
                {
                    return await HandleEmptyResultAsync(leafScan, scanId, scanTimestamp, leaf);
                }

                var existingHashes = await _hashService.GetHashesAsync(leafScan.ToPackageIdentityCommit(), requireFresh: true);
                if (existingHashes is not null)
                {
                    var records = new List<T>();
                    for (var i = 0; i < zipDirectory.Entries.Count; i++)
                    {
                        CentralDirectoryHeader? entry = zipDirectory.Entries[i];
                        var record = NewDetailsRecord(scanId, scanTimestamp, leaf);
                        record.SequenceNumber = i;
                        record.Path = entry.GetName();
                        record.FileName = Path.GetFileName(record.Path);
                        record.FileExtension = Path.GetExtension(record.Path);
                        record.TopLevelFolder = PathUtility.GetTopLevelFolder(record.Path);
                        record.CompressedLength = entry.CompressedSize;
                        record.EntryUncompressedLength = entry.UncompressedSize;

                        var entryHash = existingHashes.Value.EntryHashes[i];
                        if (entryHash is not null)
                        {
                            record.ActualUncompressedLength = entryHash.ActualCompressedLength;
                            record.SHA256 = entryHash.SHA256.ToBase64();
                            record.First16Bytes = entryHash.First16Bytes.ToBase64();
                        }
                        else
                        {
                            record.ResultType = FileRecordResultType.InvalidZipEntry;
                        }

                        records.Add(record);
                    }

                    return MakeResults(records);
                }

                var url = await GetZipUrlAsync(leafScan);
                var result = await _fileDownloader.DownloadUrlToFileAsync(
                    url,
                    TempStreamWriter.GetTempFileNameFactory(
                        leafScan.PackageId,
                        leafScan.PackageVersion,
                        "hashes",
                        FileDownloader.GetFileExtension(FileType)),
                    IncrementalHash.CreateAll,
                    CancellationToken.None);

                if (result is null)
                {
                    return await HandleEmptyResultAsync(leafScan, scanId, scanTimestamp, leaf);
                }

                await using (result.Value.Body)
                {
                    if (result.Value.Body.Type == TempStreamResultType.SemaphoreNotAvailable)
                    {
                        return DriverResult.TryAgainLater<IReadOnlyList<T>>();
                    }

                    using var zipArchive = new ZipArchive(result.Value.Body.Stream);

                    if (zipArchive.Entries.Count == 0)
                    {
                        throw new InvalidOperationException($"{FileType} ZIP archive for {leaf.PackageId} {leaf.PackageVersion} has no entries.");
                    }

                    var records = new List<T>(zipArchive.Entries.Count);
                    var entryHashes = new List<EntryHash?>(zipArchive.Entries.Count);
                    var sequenceNumber = 0;
                    var pool = ArrayPool<byte>.Shared;
                    var buffer = pool.Rent(TempStreamDirectory.DefaultBufferSize);

                    try
                    {
                        foreach (var entry in zipArchive.Entries)
                        {
                            var record = NewDetailsRecord(scanId, scanTimestamp, leaf);
                            record.SequenceNumber = sequenceNumber;
                            record.Path = entry.FullName;
                            record.FileName = Path.GetFileName(entry.FullName);
                            record.FileExtension = Path.GetExtension(entry.FullName);
                            record.TopLevelFolder = PathUtility.GetTopLevelFolder(entry.FullName);
                            record.CompressedLength = entry.CompressedLength;
                            record.EntryUncompressedLength = entry.Length;

                            entryHashes.Add(ProcessEntryStream(buffer, entry, record));
                            records.Add(record);

                            sequenceNumber++;
                        }
                    }
                    finally
                    {
                        pool.Return(buffer);
                    }

                    // We have downloaded the full ZIP archive here so we can capture the calculated hashes.
                    await _hashService.SetHashesAsync(
                        leafScan.ToPackageIdentityCommit(),
                        result.Value.Headers,
                        result.Value.Body.Hash,
                        entryHashes);

                    return MakeResults(records);
                }
            }
        }

        private async Task<DriverResult<IReadOnlyList<T>>> HandleEmptyResultAsync(
            CatalogLeafScan leafScan,
            Guid scanId,
            DateTimeOffset scanTimestamp,
            PackageDetailsCatalogLeaf leaf)
        {
            // We must clear the data related to deleted, or unavailable ZIP archives.
            await _hashService.SetHashesAsync(leafScan.ToPackageIdentityCommit(), headers: null, archiveHashes: null, entryHashes: null);

            if (NotFoundIsDeleted)
            {
                return MakeEmptyResults();
            }
            else
            {
                var record = NewDetailsRecord(scanId, scanTimestamp, leaf);
                record.ResultType = FileRecordResultType.DoesNotExist;
                return MakeResults([record]);
            }
        }

        private EntryHash? ProcessEntryStream(byte[] buffer, ZipArchiveEntry entry, T record)
        {
            try
            {
                using var stream = entry.Open();
                using var hasher = IncrementalHash.CreateSHA256();
                int read;
                long totalRead = 0;
                byte[] firstBytes = Array.Empty<byte>();
                const int firstBytesLength = 16;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (totalRead == 0)
                    {
                        firstBytes = buffer.Take(Math.Min(firstBytesLength, read)).ToArray();
                    }

                    hasher.TransformBlock(buffer, 0, read);
                    totalRead += read;
                }

                hasher.TransformFinalBlock();

                record.ActualUncompressedLength = totalRead;
                record.SHA256 = hasher.Output.SHA256.ToBase64();
                record.First16Bytes = firstBytes.ToBase64();
                return new EntryHash(totalRead, hasher.Output.SHA256, firstBytes);
            }
            catch (InvalidDataException ex)
            {
                record.ResultType = FileRecordResultType.InvalidZipEntry;
                _logger.LogInformation(ex, "{FileType} ZIP archive for {Id} {Version} has an invalid ZIP entry: {Path}", FileType, record.Id, record.Version, record.Path);
                return null;
            }
        }

        private static DriverResult<IReadOnlyList<T>> MakeResults(IReadOnlyList<T> records)
        {
            return DriverResult.Success(records);
        }

        private static DriverResult<IReadOnlyList<T>> MakeEmptyResults()
        {
            return MakeResults([]);
        }

        public async Task DestroyAsync()
        {
            await _hashService.DeleteTableAsync();
        }
    }
}
