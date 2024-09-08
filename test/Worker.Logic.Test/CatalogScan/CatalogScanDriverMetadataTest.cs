// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if ENABLE_CRYPTOAPI
using NuGet.Insights.Worker.PackageCertificateToCsv;
#endif

namespace NuGet.Insights.Worker
{
    public class CatalogScanDriverMetadataTest : BaseWorkerLogicIntegrationTest
    {
        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void StartableDriverTypes_TopologicalOrder_ReturnsAllDependenciesBeforeType(CatalogScanDriverType type)
        {
            var beforeTypes = CatalogScanDriverMetadata.StartableDriverTypes.TakeWhile(x => x != type).ToList();
            var dependencies = CatalogScanDriverMetadata.GetDependencies(type);
            Assert.All(dependencies, x => Assert.Contains(x, beforeTypes));
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void StartableDriverTypes_TopologicalOrder_ReturnsAllDependentsAfterType(CatalogScanDriverType type)
        {
            var afterTypes = CatalogScanDriverMetadata.StartableDriverTypes.SkipWhile(x => x != type).Skip(1).ToList();
            var dependents = CatalogScanDriverMetadata.GetDependents(type);
            Assert.All(dependents, x => Assert.Contains(x, afterTypes));
        }

        [Theory]
        [MemberData(nameof(LatestLeavesDriverTypesData))]
        public void GetBucketKeyFactory_ReturnsBucketKeyMatchingRecords(CatalogScanDriverType type)
        {
            var id = "NuGet.Protocol";
            var version = "6.11.0.0-BETA";
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var lowerId = id.ToLowerInvariant();

#if ENABLE_CRYPTOAPI
            var fingerpint = "WZwhyq+aBTSc7liizyZSlTOr2/v+/vNEqmA5uMp/Ulk=";
#endif

            var bucketKey = CatalogScanDriverMetadata.GetBucketKeyFactory(type)(lowerId, normalizedVersion);

            if (Host.Services.GetRequiredService<CsvRecordContainers>().TryGetRecordTypes(type, out var recordTypes))
            {
                foreach (var recordType in recordTypes)
                {
                    if (!recordType.IsAssignableTo(typeof(IAggregatedCsvRecord)))
                    {
                        continue;
                    }

                    var record = (IAggregatedCsvRecord)Activator.CreateInstance(recordType);

                    // Populate test data on the record so the bucket key can be realistic.
                    recordType.GetProperty(nameof(PackageRecord.Id))?.SetValue(record, id);
                    recordType.GetProperty(nameof(PackageRecord.LowerId))?.SetValue(record, lowerId);
                    recordType.GetProperty(nameof(PackageRecord.Version), typeof(string))?.SetValue(record, version);
                    recordType.GetProperty(nameof(PackageRecord.Identity))?.SetValue(record, PackageRecord.GetIdentity(lowerId, normalizedVersion));

#if ENABLE_CRYPTOAPI
                    recordType.GetProperty(nameof(CertificateRecord.Fingerprint))?.SetValue(record, fingerpint);
#endif

                    var recordBucketKey = record.GetBucketKey();
                    switch (record)
                    {

#if ENABLE_CRYPTOAPI
                        // This record type is produced along with with PackageCertificateRecord.
                        // These two records have different bucket strategies. We have to pick one of them so we
                        // prefer the bucket key of the PackageCertificateRecord, which has more records per package.
                        case CertificateRecord:
                            Assert.Equal(fingerpint, recordBucketKey);
                            Assert.NotEqual(bucketKey, recordBucketKey);
                            break;
#endif

                        // By default the driver's bucket key should match the bucket key of all of the records produced by the driver.
                        default:
                            Assert.NotNull(recordBucketKey);
                            Assert.NotEmpty(recordBucketKey);
                            Assert.Equal(bucketKey, record.GetBucketKey());
                            break;
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetOnlyLatestLeavesSupport_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetOnlyLatestLeavesSupport(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetBucketRangeSupport_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetBucketRangeSupport(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetUpdatedOutsideOfCatalog_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetUpdatedOutsideOfCatalog(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetDefaultMin_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetDefaultMin(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetTransitiveClosure_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetTransitiveClosure(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetDependents_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetDependents(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetDependencies_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetDependencies(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void NoDriverHasRedundantDependencies(CatalogScanDriverType type)
        {
            var directDependencies = CatalogScanDriverMetadata.GetDependencies(type);

            var transitiveDependencies = new HashSet<CatalogScanDriverType>();
            var toExplore = new Queue<CatalogScanDriverType>(directDependencies);
            while (toExplore.Count > 0)
            {
                var current = toExplore.Dequeue();
                var dependencies = CatalogScanDriverMetadata.GetDependencies(current);
                foreach (var dependency in dependencies)
                {
                    if (transitiveDependencies.Add(dependency))
                    {
                        toExplore.Enqueue(dependency);
                    }
                }
            }

            var overlap = directDependencies.Intersect(transitiveDependencies).Order().ToList();
            Assert.Empty(overlap);
        }

        public static IEnumerable<object[]> LatestLeavesDriverTypesData => CatalogScanDriverMetadata
            .StartableDriverTypes
            .Where(x => CatalogScanDriverMetadata.GetOnlyLatestLeavesSupport(x) != false)
            .Select(x => new object[] { x });

        public CatalogScanDriverMetadataTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
