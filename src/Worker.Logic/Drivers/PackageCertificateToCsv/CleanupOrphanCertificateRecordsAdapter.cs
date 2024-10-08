// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.ReferenceTracking;
using NuGet.Insights.Worker.ReferenceTracking;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class CleanupOrphanCertificateRecordsAdapter : ICleanupOrphanRecordsAdapter<CertificateRecord>
    {
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public CleanupOrphanCertificateRecordsAdapter(IOptions<NuGetInsightsWorkerSettings> options)
        {
            _options = options;
        }

        public string OperationName => "CleanupCertificateRecords";
        public string OwnerType => ReferenceTypes.Package;
        public string SubjectType => ReferenceTypes.Certificate;
        public string OwnerToSubjectTableName => _options.Value.PackageToCertificateTableName;
        public string SubjectToOwnerTableName => _options.Value.CertificateToPackageTableName;

        public IReadOnlyList<CertificateRecord> MapToOrphanRecords(IReadOnlyList<SubjectReference> subjects)
        {
            return subjects
                .Select(x => new CertificateRecord { Fingerprint = x.PartitionKey, ResultType = PackageCertificateResultType.Deleted })
                .ToList();
        }
    }
}
