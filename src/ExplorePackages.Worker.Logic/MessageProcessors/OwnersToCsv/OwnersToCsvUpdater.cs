using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.BuildVersionSet;
using Knapcode.ExplorePackages.Worker.StreamWriterUpdater;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.OwnersToCsv
{
    public class OwnersToCsvUpdater : IStreamWriterUpdater<PackageOwnerSet>
    {
        private readonly PackageOwnersClient _packageOwnersClient;
        private readonly IVersionSetProvider _versionSetProvider;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public OwnersToCsvUpdater(
            PackageOwnersClient packageOwnersClient,
            IVersionSetProvider versionSetProvider,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _packageOwnersClient = packageOwnersClient;
            _versionSetProvider = versionSetProvider;
            _options = options;
        }

        public string OperationName => "OwnersToCsv";
        public string BlobName => "owners";
        public string ContainerName => _options.Value.PackageOwnersContainerName;
        public TimeSpan Frequency => _options.Value.OwnersToCsvFrequency;
        public bool IsEnabled => _options.Value.OwnersV2Url != null;
        public bool AutoStart => _options.Value.AutoStartOwnersToCsv;

        public async Task<PackageOwnerSet> GetDataAsync()
        {
            return await _packageOwnersClient.GetPackageOwnerSetAsync();
        }

        public async Task WriteAsync(PackageOwnerSet data, StreamWriter writer)
        {
            var versionSet = await _versionSetProvider.GetAsync();

            var record = new PackageOwnerRecord { AsOfTimestamp = data.AsOfTimestamp };
            record.WriteHeader(writer);

            var idToOwners = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            await foreach (var entry in data.Owners)
            {
                if (!versionSet.DidIdEverExist(entry.Id))
                {
                    continue;
                }

                if (!idToOwners.TryGetValue(entry.Id, out var owners))
                {
                    // Only write when we move to the next ID. This ensures all of the owners of a given ID are in the same record.
                    if (idToOwners.Any())
                    {
                        WriteAndClear(writer, record, idToOwners);
                    }

                    owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    idToOwners.Add(entry.Id, owners);
                }

                owners.Add(entry.Username);
            }

            if (idToOwners.Any())
            {
                WriteAndClear(writer, record, idToOwners);
            }
        }

        private static void WriteAndClear(StreamWriter writer, PackageOwnerRecord record, Dictionary<string, HashSet<string>> idToOwners)
        {
            foreach (var pair in idToOwners)
            {
                record.LowerId = pair.Key.ToLowerInvariant();
                record.Id = pair.Key;
                record.Owners = JsonConvert.SerializeObject(pair.Value.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                record.Write(writer);
            }

            idToOwners.Clear();
        }
    }
}
