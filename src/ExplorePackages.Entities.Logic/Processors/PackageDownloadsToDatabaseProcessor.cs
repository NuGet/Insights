using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.Delta.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Entities
{
    public class PackageDownloadsToDatabaseProcessor
    {
        private readonly IPackageDownloadsClient _client;
        private readonly IPackageService _service;
        private readonly IETagService _etagService;
        private readonly IBatchSizeProvider _batchSizeProvider;
        private readonly IOptionsSnapshot<ExplorePackagesEntitiesSettings> _options;
        private readonly ILogger<PackageDownloadsToDatabaseProcessor> _logger;

        public PackageDownloadsToDatabaseProcessor(
            IPackageDownloadsClient client,
            IPackageService service,
            IETagService etagService,
            IBatchSizeProvider batchSizeProvider,
            IOptionsSnapshot<ExplorePackagesEntitiesSettings> options,
            ILogger<PackageDownloadsToDatabaseProcessor> logger)
        {
            _client = client;
            _service = service;
            _etagService = etagService;
            _batchSizeProvider = batchSizeProvider;
            _options = options;
            _logger = logger;
        }

        public async Task UpdateAsync()
        {
            var previousETag = await _etagService.GetValueAsync(ETagNames.DownloadsV1);
            string newETag = null;
            var newPath = _options.Value.DownloadsV1Path + ".new";

            var taskQueue = new TaskQueue<IReadOnlyList<PackageDownloads>>(
                workerCount: 1,
                produceAsync: async (ctx, t) =>
                {
                    newETag = await ProduceAsync(ctx, newPath, previousETag, t);
                },
                consumeAsync: ConsumeAsync,
                logger: _logger);

            await taskQueue.RunAsync();

            if (newETag != previousETag)
            {
                await _etagService.SetValueAsync(ETagNames.DownloadsV1, newETag);
                _logger.LogInformation(
                    "[CHECKPOINT] Updated {ETagName} etag to {ETagValue}.",
                    ETagNames.DownloadsV1,
                    newETag);

                var oldPath = _options.Value.DownloadsV1Path + ".old";
                SafeFileWriter.Replace(_options.Value.DownloadsV1Path, newPath, oldPath, _logger);
            }

            File.Delete(ProgressFileName);
        }

        private async Task WriteDownloadsAsync(
            string path,
            System.Collections.Generic.IAsyncEnumerator<PackageDownloads> enumerator)
        {
            var records = new List<PackageDownloads>();
            await using (enumerator)
            {
                while (await enumerator.MoveNextAsync())
                {
                    records.Add(enumerator.Current);
                }
            }

            records.Sort(new PackageDownloadsComparer(considerDownloads: true));

            using (var fileStream = new FileStream(path, FileMode.Create))
            using (var writer = new StreamWriter(fileStream))
            {
                foreach (var record in records)
                {
                    // Skip package IDs that are not valid.
                    if (!StrictPackageIdValidator.IsValid(record.Id))
                    {
                        continue;
                    }

                    await writer.WriteLineAsync(SerializeLine(record));
                }
            }
        }

        private static string SerializeLine(PackageDownloads record)
        {
            return JsonConvert.SerializeObject(new object[] { record.Id, record.Version, record.Downloads });
        }

        private PackageDownloads ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            var array = JArray.Parse(line);
            if (array.Count != 3)
            {
                throw new InvalidDataException($"Could not parse package downloads line: '{line}'.");
            }

            return new PackageDownloads(
                array[0].ToObject<string>(),
                array[1].ToObject<string>(),
                array[2].ToObject<long>());
        }

        private async Task<string> ProduceAsync(
            IProducerContext<IReadOnlyList<PackageDownloads>> producer,
            string newPath,
            string previousETag,
            CancellationToken token)
        {
            string newETag;
            await using (var packageDownloadSet = await _client.GetPackageDownloadSetAsync(previousETag))
            {
                await WriteDownloadsAsync(newPath, packageDownloadSet.Downloads);
                newETag = packageDownloadSet.ETag;
            }

            _logger.LogInformation("Done downloading the new downloads file.");

            if (newETag == previousETag)
            {
                _logger.LogInformation("The etag has not changed from the last time the file was processed. No work is necessary.");
                return newETag;
            }

            if (!File.Exists(_options.Value.DownloadsV1Path))
            {
                File.WriteAllText(_options.Value.DownloadsV1Path, string.Empty);
            }

            PackageDownloads completedUpTo = null;
            if (File.Exists(ProgressFileName))
            {
                completedUpTo = ParseLine(File.ReadAllText(ProgressFileName));
            }

            var comparer = new PackageDownloadsComparer(considerDownloads: false);
            var batch = new List<PackageDownloads>();

            using (var existingStream = new FileStream(_options.Value.DownloadsV1Path, FileMode.Open))
            using (var existingReader = new StreamReader(existingStream))
            using (var newStream = new FileStream(newPath, FileMode.Open))
            using (var newReader = new StreamReader(newStream))
            {
                var existingRecord = await ReadLineUpToAsync(existingReader, completedUpTo, comparer);
                var newRecord = await ReadLineUpToAsync(newReader, completedUpTo, comparer);

                do
                {
                    token.ThrowIfCancellationRequested();

                    var comparison = comparer.Compare(existingRecord, newRecord);
                    if (comparison == 0)
                    {
                        // The package ID and version are the same. Only consider the record if the download count has
                        // changed.
                        if (existingRecord?.Downloads != newRecord?.Downloads)
                        {
                            batch.Add(newRecord);
                        }

                        existingRecord = ParseLine(await existingReader.ReadLineAsync());
                        newRecord = ParseLine(await newReader.ReadLineAsync());
                    }
                    else if (comparison < 0)
                    {
                        // There is an extra record in the existing file, i.e. there is a missing or deleted record in
                        // the existing file. Emit the older record.
                        batch.Add(existingRecord);
                        existingRecord = ParseLine(await existingReader.ReadLineAsync());
                    }
                    else
                    {
                        // There is an extra record in the new file,. i.e. a record has been added. Emit the newer record.
                        batch.Add(newRecord);
                        newRecord = ParseLine(await newReader.ReadLineAsync());
                    }
                    
                    if (batch.Count >= _batchSizeProvider.Get(BatchSizeType.PackageDownloadsToDatabase))
                    {
                        await producer.EnqueueAsync(batch, token);
                        batch = new List<PackageDownloads>();
                    }
                }
                while (existingRecord != null || newRecord != null);

                if (batch.Any())
                {
                    await producer.EnqueueAsync(batch, token);
                }
            }

            return newETag;
        }

        private async Task<PackageDownloads> ReadLineUpToAsync(
            StreamReader reader,
            PackageDownloads upTo,
            PackageDownloadsComparer comparer)
        {
            PackageDownloads current;
            do
            {
                current = ParseLine(await reader.ReadLineAsync());
            }
            while (upTo != null && comparer.Compare(current, upTo) <= 0);

            return current;
        }

        private async Task ConsumeAsync(IReadOnlyList<PackageDownloads> packages, CancellationToken token)
        {
            if (!packages.Any())
            {
                return;
            }

            await _service.AddOrUpdatePackagesAsync(packages);

            var lastPackage = packages.Last();
            var progressContent = SerializeLine(packages.Last());
            await SafeFileWriter.WriteAsync(
                ProgressFileName,
                new MemoryStream(Encoding.UTF8.GetBytes(progressContent)),
                _logger);
            _logger.LogInformation(
                "[CHECKPOINT] Got up to {Id} {Version} with {Downloads} downloads.",
                lastPackage.Id,
                lastPackage.Version,
                lastPackage.Downloads);
        }

        private string ProgressFileName => _options.Value.DownloadsV1Path + ".progress";

        private class PackageDownloadsComparer : IComparer<PackageDownloads>
        {
            private readonly bool _considerDownloads;

            public PackageDownloadsComparer(bool considerDownloads)
            {
                _considerDownloads = considerDownloads;
            }

            public int Compare(PackageDownloads x, PackageDownloads y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }

                if (x == null)
                {
                    return 1;
                }

                if (y == null)
                {
                    return -1;
                }

                var idComparison = StringComparer.OrdinalIgnoreCase.Compare(x.Id, y.Id);
                if (idComparison != 0)
                {
                    return idComparison;
                }

                var versionComparison = StringComparer.OrdinalIgnoreCase.Compare(x.Version, y.Version);
                if (versionComparison != 0 || !_considerDownloads)
                {
                    return versionComparison;
                }

                return x.Downloads.CompareTo(y.Downloads);
            }
        }
    }
}
