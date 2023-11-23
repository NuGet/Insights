// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using NuGet.Insights;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;

namespace NuGet.Insights.Worker
{
    public class CsvRecordContainers
    {
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IReadOnlyDictionary<string, ICsvRecordStorage> _containerNameToStorage;
        private readonly IReadOnlyDictionary<Type, ICsvRecordStorage> _recordTypeToStorage;
        private readonly IReadOnlyList<string> _containerNames;
        private readonly IReadOnlyList<Type> _recordTypes;
        private readonly IReadOnlyDictionary<Type, CsvRecordProducer> _recordTypeToProducer;
        private readonly IReadOnlyDictionary<CatalogScanDriverType, IReadOnlyList<Type>> _driverTypeToRecordTypes;

        public CsvRecordContainers(
            IEnumerable<ICsvRecordStorage> csvResultStorage,
            ServiceClientFactory serviceClientFactory,
            ICatalogScanDriverFactory catalogScanDriverFactory,
            IEnumerable<IAuxiliaryFileUpdater> auxiliaryFileUpdaters,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _options = options;
            _serviceClientFactory = serviceClientFactory;
            _containerNameToStorage = csvResultStorage.ToDictionary(x => x.ContainerName);
            _recordTypeToStorage = csvResultStorage.ToDictionary(x => x.RecordType);
            _containerNames = _containerNameToStorage.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
            _recordTypes = _recordTypeToStorage.Keys.OrderBy(x => x.FullName, StringComparer.Ordinal).ToList();
            _recordTypeToProducer = ComputeCsvResultProducers(catalogScanDriverFactory, auxiliaryFileUpdaters);
            _driverTypeToRecordTypes = _recordTypeToProducer
                .Where(x => x.Value.Type == CsvRecordProducerType.CatalogScanDriver)
                .GroupBy(x => x.Value.CatalogScanDriverType.Value)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<Type>)x.Select(y => y.Key).ToList());
        }

        private IReadOnlyDictionary<Type, CsvRecordProducer> ComputeCsvResultProducers(
            ICatalogScanDriverFactory catalogScanDriverFactory,
            IEnumerable<IAuxiliaryFileUpdater> auxiliaryFileUpdaters)
        {
            var output = new Dictionary<Type, CsvRecordProducer>();

            foreach (var driverType in CatalogScanCursorService.StartableDriverTypes)
            {
                var driver = catalogScanDriverFactory.Create(driverType);
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
                output.Add(updater.RecordType, new CsvRecordProducer(CsvRecordProducerType.AuxiliaryFileUpdater, CatalogScanDriverType: null));
            }

            return output;
        }

        public async Task<IReadOnlyList<CsvRecordBlob>> GetBlobsAsync(string containerName)
        {
            var serviceClient = await _serviceClientFactory.GetBlobServiceClientAsync();
            var container = serviceClient.GetBlobContainerClient(containerName);
            var storage = _containerNameToStorage[containerName];

            try
            {
                var blobs = await container
                    .GetBlobsAsync(prefix: storage.BlobNamePrefix, traits: BlobTraits.Metadata)
                    .ToListAsync();
                return blobs
                    .Select(x => new CsvRecordBlob(
                        x.Name,
                        long.Parse(x.Metadata[StorageUtility.RawSizeBytesMetadata], CultureInfo.InvariantCulture)))
                    .ToList();
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                // Handle the missing container case.
                return Array.Empty<CsvRecordBlob>();
            }
        }

        public bool TryGetRecordTypes(CatalogScanDriverType driverType, out IReadOnlyList<Type> recordTypes)
        {
            return _driverTypeToRecordTypes.TryGetValue(driverType, out recordTypes);
        }

        public CsvRecordProducer GetProducer(Type recordType)
        {
            return _recordTypeToProducer[recordType];
        }

        public IReadOnlyList<string> ContainerNames => _containerNames;
        public IReadOnlyList<Type> RecordTypes => _recordTypes;

        public Type GetRecordType(string containerName)
        {
            if (!_containerNameToStorage.TryGetValue(containerName, out var storage))
            {
                throw new ArgumentException("The provided container name is not known.", nameof(containerName));
            }

            return storage.RecordType;
        }

        public string GetContainerName(Type recordType)
        {
            if (!_recordTypeToStorage.TryGetValue(recordType, out var storage))
            {
                throw new ArgumentException("The provided record type is not known.", nameof(recordType));
            }

            return storage.ContainerName;
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
