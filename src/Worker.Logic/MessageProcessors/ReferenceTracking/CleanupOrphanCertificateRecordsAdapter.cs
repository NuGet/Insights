// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Insights.ReferenceTracking;
using NuGet.Insights.Worker.PackageCertificateToCsv;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class CleanupOrphanCertificateRecordsAdapter : ICleanupOrphanRecordsAdapter<CertificateRecord>
    {
        public string OperationName => "CleanupCertificateRecords";
        public string OwnerType => ReferenceTypes.Package;
        public string SubjectType => ReferenceTypes.Certificate;

        public IReadOnlyList<ICsvRecordSet<CertificateRecord>> MapToOrphanRecords(IReadOnlyList<SubjectReference> subjects)
        {
            return subjects
                .Select(x => new CertificateRecord { Fingerprint = x.PartitionKey, ResultType = PackageCertificateResultType.Deleted })
                .Select(x => new CsvRecordSet<CertificateRecord>(x.Fingerprint, new[] { x }))
                .ToList();
        }
    }
}
