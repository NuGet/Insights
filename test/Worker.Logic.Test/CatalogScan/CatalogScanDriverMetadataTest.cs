// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class CatalogScanDriverMetadataTest : BaseWorkerLogicIntegrationTest
    {
        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void StartableDriverTypes_TopologicalOrder_ReturnsAllDependenciesBeforeType(string typeName)
        {
            var type = CatalogScanDriverType.Parse(typeName);
            var beforeTypes = CatalogScanDriverMetadata.StartableDriverTypes.TakeWhile(x => x != type).ToList();
            var dependencies = CatalogScanDriverMetadata.GetDependencies(type);
            Assert.All(dependencies, x => Assert.Contains(x, beforeTypes));
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void StartableDriverTypes_TopologicalOrder_ReturnsAllDependentsAfterType(string typeName)
        {
            var type = CatalogScanDriverType.Parse(typeName);
            var afterTypes = CatalogScanDriverMetadata.StartableDriverTypes.SkipWhile(x => x != type).Skip(1).ToList();
            var dependents = CatalogScanDriverMetadata.GetDependents(type);
            Assert.All(dependents, x => Assert.Contains(x, afterTypes));
        }

        private static List<CatalogScanDriverType> PackageRecordDriverTypes { get; } = CatalogScanDriverMetadata
            .StartableDriverTypes
            .Where(x => CatalogScanDriverMetadata.GetOnlyLatestLeavesSupport(x) != false)
            .ToList();

        public static IEnumerable<object[]> GetBucketKeyFactory_ReturnsBucketKeyMatchingRecordsData => PackageRecordDriverTypes
            .Select(x => new object[] { x.ToString() });

        [Theory]
        [MemberData(nameof(GetBucketKeyFactory_ReturnsBucketKeyMatchingRecordsData))]
        public void GetBucketKeyFactory_ReturnsBucketKeyMatchingRecords(string typeName)
        {
            var type = CatalogScanDriverType.Parse(typeName);

            VerifyBucketKeyMatchesRecords(type, VerifyPackageRecordBucketKey);
        }

        private static void VerifyPackageRecordBucketKey(Type recordType, string bucketKey, IAggregatedCsvRecord record)
        {
            // By default the driver's bucket key should match the bucket key of all of the records produced by the driver.
            var recordBucketKey = record.GetBucketKey();
            Assert.NotNull(recordBucketKey);
            Assert.NotEmpty(recordBucketKey);
            Assert.Equal(bucketKey, recordBucketKey);
        }

        private static void VerifyBucketKeyMatchesRecords(
            CatalogScanDriverType type,
            Action<Type, string, IAggregatedCsvRecord> verifyRecordBucketKey)
        {
            var id = "NuGet.Protocol";
            var version = "6.11.0.0-BETA";
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var lowerId = id.ToLowerInvariant();

            var bucketKey = CatalogScanDriverMetadata.GetBucketKeyFactory(type)(lowerId, normalizedVersion);
            var recordTypes = CatalogScanDriverMetadata.GetRecordTypes(type);

            if (recordTypes is not null)
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

                    verifyRecordBucketKey(recordType, bucketKey, record);
                }
            }
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetRuntimeType_ReturnsBatchOrNonBatchDriver(string typeName)
        {
            var type = CatalogScanDriverType.Parse(typeName);
            var runtimeType = CatalogScanDriverMetadata.GetRuntimeType(type);

            if (CatalogScanDriverMetadata.IsBatchDriver(type))
            {
                Assert.True(runtimeType.IsAssignableTo(typeof(ICatalogLeafScanBatchDriver)));
            }
            else
            {
                Assert.True(runtimeType.IsAssignableTo(typeof(ICatalogLeafScanNonBatchDriver)));
            }
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void NoDriverHasRedundantDependencies(string typeName)
        {
            var type = CatalogScanDriverType.Parse(typeName);
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

        [Fact]
        public void GetTitleReturnsUppercaseCSV()
        {
            var actual = CatalogScanDriverMetadata.GetTitle(CatalogScanDriverType.PackageAssetToCsv);
            Assert.Equal("Package asset to CSV", actual);
        }

        [Fact]
        public void GetTitleReturnsTitleCase()
        {
            var actual = CatalogScanDriverMetadata.GetTitle(CatalogScanDriverType.LoadPackageArchive);
            Assert.Equal("Load package archive", actual);
        }

        public CatalogScanDriverMetadataTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
