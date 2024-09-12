// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.PackageCertificateToCsv;

namespace NuGet.Insights.Worker
{
    public partial class CatalogScanDriverMetadataTest : BaseWorkerLogicIntegrationTest
    {
        static CatalogScanDriverMetadataTest()
        {
            PackageRecordDriverTypes.Remove(CatalogScanDriverType.PackageCertificateToCsv);
        }

        [Fact]
        public void PackageCertificateToCsv_ReturnsBucketKeyMatchingRecordsData()
        {
            var type = CatalogScanDriverType.PackageCertificateToCsv;
            var fingerpint = "WZwhyq+aBTSc7liizyZSlTOr2/v+/vNEqmA5uMp/Ulk=";

            VerifyBucketKeyMatchesRecords(type, (recordType, bucketKey, record) =>
            {
                recordType.GetProperty(nameof(CertificateRecord.Fingerprint))?.SetValue(record, fingerpint);

                var recordBucketKey = record.GetBucketKey();

                if (record is CertificateRecord)
                {
                    // This record type is produced along with with PackageCertificateRecord.
                    // These two records have different bucket strategies. We have to pick one of them so we
                    // prefer the bucket key of the PackageCertificateRecord, which has more records per package.
                    Assert.Equal(fingerpint, recordBucketKey);
                    Assert.NotEqual(bucketKey, recordBucketKey);
                }
                else
                {
                    VerifyPackageRecordBucketKey(recordType, bucketKey, record);
                }
            });
        }
    }
}
