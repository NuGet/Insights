// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Buffers;
using System.IO.Compression;

#nullable enable

namespace NuGet.Insights.Worker
{
    public abstract class ZipArchiveEntryHashToCsvDriver<T> : ICatalogLeafToCsvDriver<T>, ICsvResultStorage<T>
        where T : FileRecord, IAggregatedCsvRecord<T>
    {
        private readonly CatalogClient _catalogClient;
        private readonly FileDownloader _fileDownloader;
        private readonly PackageSpecificHashService _hashService;
        private readonly ILogger _logger;

        public ZipArchiveEntryHashToCsvDriver(
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
        protected abstract bool UrlNotFoundIsDeleted { get; }
        protected abstract ArtifactFileType FileType { get; }

        public async Task InitializeAsync()
        {
            await _hashService.InitializeAsync();
            await InternalInitializeAsync();
        }

        protected abstract T NewDeleteRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf);
        protected abstract T NewDetailsRecord(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf);
        protected abstract Task<string?> GetZipUrlAsync(CatalogLeafScan leafScan);
        protected abstract Task InternalInitializeAsync();

        public async Task<DriverResult<IReadOnlyList<T>>> ProcessLeafAsync(CatalogLeafScan leafScan)
        {
            var scanId = Guid.NewGuid();
            var scanTimestamp = DateTimeOffset.UtcNow;

            if (leafScan.LeafType == CatalogLeafType.PackageDelete)
            {
                var leaf = (PackageDeleteCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                // We must clear the data related to deleted ZIP archives.
                await _hashService.SetHashesAsync(leafScan.ToPackageIdentityCommit(), headers: null, hashes: null);

                return MakeResults([NewDeleteRecord(scanId, scanTimestamp, leaf)]);
            }
            else
            {
                var leaf = (PackageDetailsCatalogLeaf)await _catalogClient.GetCatalogLeafAsync(leafScan.LeafType, leafScan.Url);

                var hashes = await _hashService.GetHashesAsync(leafScan.ToPackageIdentityCommit(), requireFresh: true);
                if (hashes is not null)
                {
                    // The data is already up to date. No-op.
                    return MakeEmptyResults();
                }

                var url = await GetZipUrlAsync(leafScan);
                if (url is null)
                {
                    return await HandleEmptyResultAsync(leafScan, scanId, scanTimestamp, leaf);
                }

                var result = await _fileDownloader.DownloadUrlToFileAsync(
                    url,
                    TempStreamWriter.GetTempFileNameFactory(
                        leafScan.PackageId,
                        leafScan.PackageVersion,
                        "hashes",
                        FileDownloader.GetFileExtension(FileType)),
                    CancellationToken.None);

                if (result is null)
                {
                    return await HandleEmptyResultAsync(leafScan, scanId, scanTimestamp, leaf);
                }

                using (result.Value.Body)
                {
                    if (result.Value.Body.Type == TempStreamResultType.SemaphoreNotAvailable)
                    {
                        return DriverResult.TryAgainLater<IReadOnlyList<T>>();
                    }

                    // We have downloaded the full ZIP archive here so we can capture the calculated hashes.
                    await _hashService.SetHashesAsync(leafScan.ToPackageIdentityCommit(), result.Value.Headers, result.Value.Body.Hash);

                    using var zipArchive = new ZipArchive(result.Value.Body.Stream);

                    if (zipArchive.Entries.Count == 0)
                    {
                        throw new InvalidOperationException($"{FileType} ZIP archive for {leaf.PackageId} {leaf.PackageVersion} has no entries.");
                    }

                    var records = new List<T>(zipArchive.Entries.Count);
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

                            ProcessEntryStream(buffer, entry, record);

                            records.Add(record);

                            sequenceNumber++;
                        }
                    }
                    finally
                    {
                        pool.Return(buffer);
                    }

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
            await _hashService.SetHashesAsync(leafScan.ToPackageIdentityCommit(), headers: null, hashes: null);

            if (UrlNotFoundIsDeleted)
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

        private void ProcessEntryStream(byte[] buffer, ZipArchiveEntry entry, T record)
        {
            try
            {
                using var stream = entry.Open();
                using var hasher = IncrementalHash.CreateAll();
                int read;
                long totalRead = 0;
                string? first64 = null;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (first64 is null)
                    {
                        first64 = Convert.ToBase64String(buffer, 0, Math.Min(64, read));
                    }

                    hasher.TransformBlock(buffer, 0, read);
                    totalRead += read;
                }

                hasher.TransformFinalBlock();

                record.ActualUncompressedLength = totalRead;

                record.MD5 = Convert.ToBase64String(hasher.Output.MD5);
                record.SHA1 = Convert.ToBase64String(hasher.Output.SHA1);
                record.SHA256 = Convert.ToBase64String(hasher.Output.SHA256);
                record.SHA512 = Convert.ToBase64String(hasher.Output.SHA512);

                record.First64 = first64;
            }
            catch (InvalidDataException ex)
            {
                record.ResultType = FileRecordResultType.InvalidZipEntry;
                _logger.LogInformation(ex, "{FileType} ZIP archive for {Id} {Version} has an invalid ZIP entry: {Path}", FileType, record.Id, record.Version, record.Path);
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
