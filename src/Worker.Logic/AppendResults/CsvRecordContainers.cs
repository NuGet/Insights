// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Frozen;
using Azure;
using Azure.Storage.Blobs.Models;
using NuGet.Insights;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;

#nullable enable

namespace NuGet.Insights.Worker
{
    public record CsvRecordContainerInfo(string ContainerName, Type RecordType, string BlobNamePrefix);

    public class CsvRecordContainers
    {
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly List<CsvRecordContainerInfo> _containers;
        private readonly FrozenDictionary<string, CsvRecordContainerInfo> _containerNameToInfo;
        private readonly FrozenDictionary<Type, CsvRecordContainerInfo> _recordTypeToInfo;
        private readonly IReadOnlyList<string> _containerNames;
        private readonly IReadOnlyList<Type> _recordTypes;
        private readonly FrozenDictionary<Type, CsvRecordProducer> _recordTypeToProducer;

        public CsvRecordContainers(
            IEnumerable<CsvRecordContainerInfo> containerInfo,
            ServiceClientFactory serviceClientFactory,
            ICatalogScanDriverFactory catalogScanDriverFactory,
            IEnumerable<IAuxiliaryFileUpdater> auxiliaryFileUpdaters,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _options = options;
            _serviceClientFactory = serviceClientFactory;
            _containers = containerInfo.ToList();
            _containerNameToInfo = _containers.ToDictionary(x => x.ContainerName).ToFrozenDictionary();
            _recordTypeToInfo = _containers.ToDictionary(x => x.RecordType).ToFrozenDictionary();
            _containerNames = _containerNameToInfo.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
            _recordTypes = _recordTypeToInfo.Keys.OrderBy(x => x.FullName, StringComparer.Ordinal).ToList();
            _recordTypeToProducer = ComputeCsvResultProducers(catalogScanDriverFactory, auxiliaryFileUpdaters);
        }

        private FrozenDictionary<Type, CsvRecordProducer> ComputeCsvResultProducers(
            ICatalogScanDriverFactory catalogScanDriverFactory,
            IEnumerable<IAuxiliaryFileUpdater> auxiliaryFileUpdaters)
        {
            var output = new Dictionary<Type, CsvRecordProducer>();

            foreach (var driverType in CatalogScanDriverMetadata.StartableDriverTypes)
            {
                ICatalogScanDriver? driver = catalogScanDriverFactory.CreateNonBatchDriverOrNull(driverType);
                if (driver is null)
                {
                    driver = catalogScanDriverFactory.CreateBatchDriverOrNull(driverType);
                }

                if (driver is null)
                {
                    throw new InvalidOperationException("No driver implementation could be found.");
                }

                var recordTypes = driver
                    .GetType()
                    .GenericTypeArguments
                    .Where(x => x.IsAssignableTo(typeof(ICsvRecord)))
                    .ToList();

                foreach (var recordType in recordTypes)
                {
                    output.Add(recordType, new CsvRecordProducer(CsvRecordProducerType.CatalogScanDriver, driverType));
                }
            }

            foreach (var updater in auxiliaryFileUpdaters)
            {
                var recordType = updater
                    .GetType()
                    .GetInterfaces()
                    .Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IAuxiliaryFileUpdater<,>))
                    .Single()
                    .GenericTypeArguments
                    .Where(x => x.IsAssignableTo(typeof(ICsvRecord)))
                    .Single();

                output.Add(recordType, new CsvRecordProducer(CsvRecordProducerType.AuxiliaryFileUpdater, CatalogScanDriverType: null));
            }

            return output.ToFrozenDictionary();
        }

        public async Task<IReadOnlyList<CsvRecordBlob>> GetBlobsAsync(string containerName)
        {
            var serviceClient = await _serviceClientFactory.GetBlobServiceClientAsync();
            var container = serviceClient.GetBlobContainerClient(containerName);
            var storage = _containerNameToInfo[containerName];

            try
            {
                var blobs = await container
                    .GetBlobsAsync(prefix: storage.BlobNamePrefix, traits: BlobTraits.Metadata)
                    .ToListAsync();
                return blobs
                    .Select(x => new CsvRecordBlob(containerName, x))
                    .ToList();
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                // Handle the missing container case.
                return Array.Empty<CsvRecordBlob>();
            }
        }

        public CsvRecordProducer GetProducer(Type recordType)
        {
            return _recordTypeToProducer[recordType];
        }

        public IReadOnlyList<string> ContainerNames => _containerNames;
        public IReadOnlyList<Type> RecordTypes => _recordTypes;
        public IReadOnlyList<CsvRecordContainerInfo> ContainerInfo => _containers;

        public Type GetRecordType(string containerName)
        {
            if (!_containerNameToInfo.TryGetValue(containerName, out var info))
            {
                throw new ArgumentException("The provided container name is not known.", nameof(containerName));
            }

            return info.RecordType;
        }

        public bool TryGetInfoByRecordType<T>([NotNullWhen(true)] out CsvRecordContainerInfo? info) where T : ICsvRecord<T>
        {
            return TryGetInfoByRecordType(typeof(T), out info);
        }

        public bool TryGetInfoByRecordType(Type recordType, [NotNullWhen(true)] out CsvRecordContainerInfo? info)
        {
            return _recordTypeToInfo.TryGetValue(recordType, out info);
        }

        public CsvRecordContainerInfo GetInfoByRecordType<T>() where T : ICsvRecord<T>
        {
            return GetInfoByRecordType(typeof(T));
        }

        public CsvRecordContainerInfo GetInfoByRecordType(Type recordType)
        {
            if (!TryGetInfoByRecordType(recordType, out var info))
            {
                throw new ArgumentException("The provided record type is not known.", nameof(recordType));
            }

            return info;
        }

        public string GetContainerName(Type recordType)
        {
            return GetInfoByRecordType(recordType).ContainerName;
        }

        public string GetDefaultKustoTableName(Type recordType)
        {
            return GetDefaultKustoTableName(GetContainerName(recordType));
        }

        public string GetTempKustoTableName(string containerName)
        {
            return string.Format(CultureInfo.InvariantCulture, _options.Value.KustoTempTableNameFormat, GetKustoTableName(containerName));
        }

        public string GetOldKustoTableName(string containerName)
        {
            return string.Format(CultureInfo.InvariantCulture, _options.Value.KustoOldTableNameFormat, GetKustoTableName(containerName));
        }

        public string GetKustoTableName(string containerName)
        {
            return string.Format(CultureInfo.InvariantCulture, _options.Value.KustoTableNameFormat, GetDefaultKustoTableName(containerName));
        }

        public string GetDefaultKustoTableName(string containerName)
        {
            var recordType = GetRecordType(containerName);
            var defaultTableName = KustoDDL.TypeToDefaultTableName[recordType];
            return defaultTableName;
        }
    }
}
