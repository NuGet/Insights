// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.PackageSignatureToCsv;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class FingerprintSHA256KustoValidationProvider : BaseKustoValidationProvider, IKustoValidationProvider
    {
        public FingerprintSHA256KustoValidationProvider(
            IEnumerable<ICsvRecordStorage> csvResultStorage,
            CsvRecordContainers containers)
            : base(csvResultStorage, containers)
        {
        }

        public async Task<IReadOnlyList<KustoValidation>> GetValidationsAsync()
        {
            var packageSignatureStorage = _typeToStorage[typeof(PackageSignature)];
            var certificatesStorage = _typeToStorage[typeof(CertificateRecord)];
            if (!await HasBlobsAsync(packageSignatureStorage) || !await HasBlobsAsync(certificatesStorage))
            {
                return [];
            }

            var leftTable = _containers.GetTempKustoTableName(packageSignatureStorage.ContainerName);
            var rightTable = _containers.GetTempKustoTableName(certificatesStorage.ContainerName);
            var column = nameof(CertificateRecord.FingerprintSHA256Hex);
            var joinQuery = @$"{leftTable}
| mv-expand {column} = pack_array(
    AuthorSHA256,
    AuthorTimestampSHA256,
    RepositorySHA256,
    RepositoryTimestampSHA256) to typeof(string)
| where isnotempty({column})
| distinct {column}
| join kind=leftouter {rightTable}";

            return [GetLeftRightValidation(leftTable, rightTable, column, "left outer", joinQuery)];
        }
    }
}
