using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageDownloadsToDatabaseProcessor
    {
        private readonly IPackageDownloadsClient _client;
        private readonly IPackageService _service;
        private readonly IETagService _etagService;
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _settings;
        private readonly ILogger<PackageDownloadsToDatabaseProcessor> _logger;

        public PackageDownloadsToDatabaseProcessor(
            IPackageDownloadsClient client,
            IPackageService service,
            IETagService etagService,
            IOptionsSnapshot<ExplorePackagesSettings> settings,
            ILogger<PackageDownloadsToDatabaseProcessor> logger)
        {
            _client = client;
            _service = service;
            _etagService = etagService;
            _settings = settings;
            _logger = logger;
        }

        public async Task UpdateAsync()
        {
            var previousETag = await _etagService.GetValueAsync(ETagNames.DownloadsV1);

            var taskQueue = new TaskQueue<IReadOnlyList<PackageDownloads>>(
                workerCount: 1,
                workAsync: ConsumeAsync);

            taskQueue.Start();

            var newPath = _settings.Value.DownloadsV1Path + ".new";
            var newETag = await ProduceAsync(newPath, previousETag, taskQueue);

            await taskQueue.CompleteAsync();

            if (newETag != previousETag)
            {
                await _etagService.SetValueAsync(ETagNames.DownloadsV1, newETag);

                var oldPath = _settings.Value.DownloadsV1Path + ".old";
                SafeFileWriter.Replace(_settings.Value.DownloadsV1Path, newPath, oldPath, _logger);
            }
        }

        private async Task WriteDownloadsAsync(string path, IAsyncEnumerator<PackageDownloads> enumerator)
        {
            var records = new List<PackageDownloads>();
            using (enumerator)
            {
                while (await enumerator.MoveNext())
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
                    var line = JsonConvert.SerializeObject(new object[] { record.Id, record.Version, record.Downloads });
                    await writer.WriteLineAsync(line);
                }
            }
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

        private async Task<string> ProduceAsync(string newPath, string previousETag, TaskQueue<IReadOnlyList<PackageDownloads>> taskQueue)
        {
            string newETag;
            using (var packageDownloadSet = await _client.GetPackageDownloadSetAsync(previousETag))
            {
                await WriteDownloadsAsync(newPath, packageDownloadSet.Downloads);
                newETag = packageDownloadSet.ETag;
            }

            if (newETag == previousETag)
            {
                return newETag;
            }

            if (!File.Exists(_settings.Value.DownloadsV1Path))
            {
                File.WriteAllText(_settings.Value.DownloadsV1Path, string.Empty);
            }

            var comparer = new PackageDownloadsComparer(considerDownloads: false);
            var batch = new List<PackageDownloads>();

            using (var existingStream = new FileStream(_settings.Value.DownloadsV1Path, FileMode.Open))
            using (var existingReader = new StreamReader(existingStream))
            using (var newStream = new FileStream(newPath, FileMode.Open))
            using (var newReader = new StreamReader(newStream))
            {
                var existingRecord = ParseLine(await existingReader.ReadLineAsync());
                var newRecord = ParseLine(await newReader.ReadLineAsync());

                do
                {
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
                    
                    if (batch.Count >= 20000)
                    {
                        taskQueue.Enqueue(batch);
                        batch = new List<PackageDownloads>();
                    }
                }
                while (existingRecord != null || newRecord != null);

                if (batch.Any())
                {
                    taskQueue.Enqueue(batch);
                }
            }

            return newETag;
        }

        private async Task ConsumeAsync(IReadOnlyList<PackageDownloads> packages)
        {
            await _service.AddOrUpdatePackagesAsync(packages);
        }

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
