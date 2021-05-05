using System;
using System.Collections.Generic;
using System.Linq;
using Knapcode.ExplorePackages.Worker.CatalogLeafItemToCsv;
using Knapcode.ExplorePackages.Worker.DownloadsToCsv;
using Knapcode.ExplorePackages.Worker.NuGetPackageExplorerToCsv;
using Knapcode.ExplorePackages.Worker.OwnersToCsv;
using Knapcode.ExplorePackages.Worker.PackageArchiveToCsv;
using Knapcode.ExplorePackages.Worker.PackageAssemblyToCsv;
using Knapcode.ExplorePackages.Worker.PackageAssetToCsv;
using Knapcode.ExplorePackages.Worker.PackageManifestToCsv;
using Knapcode.ExplorePackages.Worker.PackageSignatureToCsv;
using Knapcode.ExplorePackages.Worker.PackageVersionToCsv;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class CsvRecordContainers
    {
        private static readonly IReadOnlyDictionary<Type, Func<ExplorePackagesWorkerSettings, string>> RecordTypeToGetContainerName = new Dictionary<Type, Func<ExplorePackagesWorkerSettings, string>>
        {
            { RecordType<CatalogLeafItemRecord>(), x => x.CatalogLeafItemContainerName },
            { RecordType<NuGetPackageExplorerFile>(), x => x.NuGetPackageExplorerFileContainerName },
            { RecordType<NuGetPackageExplorerRecord>(), x => x.NuGetPackageExplorerContainerName },
            { RecordType<PackageArchiveEntry>(), x => x.PackageArchiveEntryContainerName },
            { RecordType<PackageArchiveRecord>(), x => x.PackageArchiveContainerName },
            { RecordType<PackageAssembly>(), x => x.PackageAssemblyContainerName },
            { RecordType<PackageAsset>(), x => x.PackageAssetContainerName },
            { RecordType<PackageDownloadRecord>(), x => x.PackageDownloadsContainerName },
            { RecordType<PackageManifestRecord>(), x => x.PackageManifestContainerName },
            { RecordType<PackageOwnerRecord>(), x => x.PackageOwnersContainerName },
            { RecordType<PackageSignature>(), x => x.PackageSignatureContainerName },
            { RecordType<PackageVersionRecord>(), x => x.PackageVersionContainerName },
        };

        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly IReadOnlyDictionary<string, Type> _containerNameToRecordType;

        public CsvRecordContainers(IOptions<ExplorePackagesWorkerSettings> options)
        {
            _options = options;
            _containerNameToRecordType = RecordTypeToGetContainerName.ToDictionary(x => x.Value(_options.Value), x => x.Key);
        }

        private static Type RecordType<T>() where T : ICsvRecord
        {
            return typeof(T);
        }

        public IReadOnlyList<string> GetContainerNames()
        {
            return RecordTypeToGetContainerName
                .Values
                .Select(x => x(_options.Value))
                .ToList();
        }

        public Type GetRecordType(string containerName)
        {
            return _containerNameToRecordType[containerName];
        }
    }
}
