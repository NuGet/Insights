// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.KustoIngestion;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class FingerprintKustoValidationProvider : BaseKustoValidationProvider, IKustoValidationProvider
    {
        public FingerprintKustoValidationProvider(
            IEnumerable<ICsvRecordStorage> csvResultStorage,
            CsvRecordContainers containers)
            : base(csvResultStorage, containers)
        {
        }

        public async Task<IReadOnlyList<KustoValidation>> GetValidationsAsync()
        {
            return await GetSetValidationsAsync(nameof(CertificateRecord.Fingerprint), required: false);
        }
    }
}
