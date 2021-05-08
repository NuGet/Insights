using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class CsvResultStorageContainers
    {
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly IReadOnlyDictionary<string, Type> _containerNameToRecordType;
        private readonly IReadOnlyList<string> _containerNames;

        public CsvResultStorageContainers(
            IEnumerable<ICsvResultStorage> resultStorages,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _options = options;
            _containerNameToRecordType = resultStorages.ToDictionary(x => x.ResultContainerName, x => x.RecordType);
            _containerNames = _containerNameToRecordType.Keys.OrderBy(x => x).ToList();
        }

        public IReadOnlyList<string> GetContainerNames()
        {
            return _containerNames;
        }

        public Type GetRecordType(string containerName)
        {
            return _containerNameToRecordType[containerName];
        }

        public string GetTempKustoTableName(string containerName)
        {
            return GetKustoTableName(containerName) + "_Temp";
        }

        public string GetKustoTableName(string containerName)
        {
            var recordType = GetRecordType(containerName);
            var defaultTableName = KustoDDL.TypeToDefaultTableName[recordType];
            return string.Format(_options.Value.KustoTableNameFormat, defaultTableName);
        }
    }
}
