// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.PackageSignatureToCsv;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class FingerprintSHA256KustoValidationProvider : BaseKustoValidationProvider, IKustoValidationProvider
    {
        public FingerprintSHA256KustoValidationProvider(CsvRecordContainers containers) : base(containers)
        {
        }

        public async Task<IReadOnlyList<KustoValidation>> GetValidationsAsync()
        {
            var packageSignatureInfo = _containers.GetInfoByRecordType<PackageSignature>();
            var certificatesInfo = _containers.GetInfoByRecordType<CertificateRecord>();
            if (!await HasBlobsAsync(packageSignatureInfo) || !await HasBlobsAsync(certificatesInfo))
            {
                return [];
            }

            var leftTable = _containers.GetTempKustoTableName(packageSignatureInfo.ContainerName);
            var rightTable = _containers.GetTempKustoTableName(certificatesInfo.ContainerName);
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
