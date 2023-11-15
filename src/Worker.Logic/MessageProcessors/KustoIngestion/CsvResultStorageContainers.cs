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

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class CsvResultStorageContainers
    {
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IReadOnlyDictionary<string, ICsvResultStorage> _containerNameToStorage;
        private readonly IReadOnlyList<string> _containerNames;

        public CsvResultStorageContainers(
            IEnumerable<ICsvResultStorage> csvResultStorage,
            ServiceClientFactory serviceClientFactory,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _options = options;
            _serviceClientFactory = serviceClientFactory;
            _containerNameToStorage = csvResultStorage.ToDictionary(x => x.ContainerName);
            _containerNames = _containerNameToStorage.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        public async Task<IReadOnlyList<CsvResultBlob>> GetBlobsAsync(string containerName)
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
                    .Select(x => new CsvResultBlob(
                        x.Name,
                        long.Parse(x.Metadata[StorageUtility.RawSizeBytesMetadata], CultureInfo.InvariantCulture)))
                    .ToList();
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                // Handle the missing container case.
                return Array.Empty<CsvResultBlob>();
            }
        }

        public IReadOnlyList<string> GetContainerNames()
        {
            return _containerNames;
        }

        public Type GetRecordType(string containerName)
        {
            return _containerNameToStorage[containerName].RecordType;
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
