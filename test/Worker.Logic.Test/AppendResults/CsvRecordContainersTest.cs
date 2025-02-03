// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using NuGet.Insights.Worker.CatalogDataToCsv;
using NuGet.Insights.Worker.DownloadsToCsv;
using NuGet.Insights.Worker.ExcludedPackagesToCsv;
using NuGet.Insights.Worker.GitHubUsageToCsv;
using NuGet.Insights.Worker.OwnersToCsv;
using NuGet.Insights.Worker.PopularityTransfersToCsv;
using NuGet.Insights.Worker.VerifiedPackagesToCsv;

namespace NuGet.Insights.Worker
{
    public class CsvRecordContainersTest : BaseWorkerLogicIntegrationTest
    {
        private static readonly IReadOnlySet<Type> KnownAuxiliaryFileRecordTypes = new HashSet<Type>
        {
            typeof(PackageDownloadRecord),
            typeof(PackageOwnerRecord),
            typeof(VerifiedPackageRecord),
            typeof(ExcludedPackageRecord),
            typeof(PopularityTransfersRecord),
            typeof(GitHubUsageRecord),
        };

        [Theory]
        [MemberData(nameof(RecordTypesData))]
        public void ReturnsProducerForAllRecordTypes(Type recordType)
        {
            var expectedProducerType = KnownAuxiliaryFileRecordTypes.Contains(recordType)
                ? CsvRecordProducerType.AuxiliaryFileUpdater
                : CsvRecordProducerType.CatalogScanDriver;

            var producer = CsvRecordContainers.GetProducer(recordType);

            Assert.Equal(expectedProducerType, producer.Type);
            switch (expectedProducerType)
            {
                case CsvRecordProducerType.AuxiliaryFileUpdater:
                    Assert.Null(producer.CatalogScanDriverType);
                    break;
                case CsvRecordProducerType.CatalogScanDriver:
                    Assert.NotNull(producer.CatalogScanDriverType);
                    Assert.Contains(producer.CatalogScanDriverType.Value, CatalogScanDriverMetadata.StartableDriverTypes);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        [Theory]
        [MemberData(nameof(RecordTypesData))]
        public void RoundTripsAllRecordTypesThroughContainerName(Type recordType)
        {
            var containerName = CsvRecordContainers.GetContainerName(recordType);

            var roundTrip = CsvRecordContainers.GetRecordType(containerName);

            Assert.Equal(recordType, roundTrip);
        }

        [Fact]
        public void ReturnsUniqueTypePerContainer()
        {
            var types = new HashSet<Type>();

            foreach (var containerName in CsvRecordContainers.ContainerNames)
            {
                var type = CsvRecordContainers.GetRecordType(containerName);
                Assert.DoesNotContain(type, types);
                types.Add(type);
            }
        }

        [Fact]
        public void ReturnsUniqueKustoTableNamePerContainer()
        {
            var kustoTableNames = new HashSet<string>();

            foreach (var containerName in CsvRecordContainers.ContainerNames)
            {
                var kustoTableName = CsvRecordContainers.GetKustoTableName(containerName);
                Assert.DoesNotContain(kustoTableName, kustoTableNames);
                kustoTableNames.Add(kustoTableName);
            }
        }

        [Fact]
        public void ReturnsSomeContainerNames()
        {
            Assert.NotEmpty(CsvRecordContainers.ContainerNames);
        }

        [Fact]
        public void CountsMatch()
        {
            Assert.Equal(NuGetInsightsWorkerLogicKustoDDL.TypeToDDL.Count, CsvRecordContainers.ContainerNames.Count);
            Assert.Equal(NuGetInsightsWorkerLogicKustoDDL.TypeToDefaultTableName.Count, CsvRecordContainers.ContainerNames.Count);
            Assert.Equal(CsvRecordContainers.RecordTypes.Count, CsvRecordContainers.ContainerNames.Count);
        }

        [Theory]
        [MemberData(nameof(AuxiliaryFileRecordTypesData))]
        public void AuxiliaryRecordTypesAreIAuxiliaryFileRecords(Type recordType)
        {
            var producer = CsvRecordContainers.GetProducer(recordType);

            Assert.Equal(CsvRecordProducerType.AuxiliaryFileUpdater, producer.Type);
            Assert.True(recordType.IsAssignableTo(typeof(IAuxiliaryFileCsvRecord<>).MakeGenericType(recordType)));
            Assert.Throws<ArgumentException>(() => recordType.IsAssignableTo(typeof(IAggregatedCsvRecord<>).MakeGenericType(recordType)));
        }

        [Theory]
        [MemberData(nameof(CatalogScanRecordTypesData))]
        public void CatalogScanRecordTypesAreIAggregatedRecord(Type recordType)
        {
            var producer = CsvRecordContainers.GetProducer(recordType);

            Assert.Equal(CsvRecordProducerType.CatalogScanDriver, producer.Type);
            Assert.True(recordType.IsAssignableTo(typeof(IAggregatedCsvRecord<>).MakeGenericType(recordType)));
            Assert.Throws<ArgumentException>(() => recordType.IsAssignableTo(typeof(IAuxiliaryFileCsvRecord<>).MakeGenericType(recordType)));
        }

        [Theory]
        [MemberData(nameof(RecordTypesData))]
        public void RecordIsUniquelyIdentifiedByKeyFields(Type recordType)
        {
            var keyComparer = GetKeyComparer(recordType);
            var keyFields = GetKeyFields(recordType);
            var recordA = PopulateFields(Activator.CreateInstance(recordType), keyFields, seed: 0);
            var recordB = PopulateFields(Activator.CreateInstance(recordType), keyFields, seed: 0);
            var recordC = PopulateFields(Activator.CreateInstance(recordType), keyFields, seed: 1);

            var equalsMethod = GetEqualsMethod(recordType, keyComparer);
            Assert.NotNull(equalsMethod);

            Assert.True((bool)equalsMethod.Invoke(keyComparer, [recordA, recordB]), "recordA should equal recordB");
            Assert.False((bool)equalsMethod.Invoke(keyComparer, [recordA, recordC]), "recordA should not equal recordC");
            Assert.False((bool)equalsMethod.Invoke(keyComparer, [recordB, recordC]), "recordB should not equal recordC");
        }

        [Theory]
        [MemberData(nameof(RecordTypeKeyFieldCombinationsSubsetsData))]
        public void AllKeyFieldsAreNeededForEquality(Type recordType, string[] keyFieldSubset)
        {
            var keyComparer = GetKeyComparer(recordType);
            var extraKeyFields = GetKeyFields(recordType).Except(keyFieldSubset).ToArray();
            var recordA = PopulateFields(Activator.CreateInstance(recordType), extraKeyFields, seed: 0);
            var recordB = PopulateFields(Activator.CreateInstance(recordType), extraKeyFields, seed: 0);
            var recordC = PopulateFields(Activator.CreateInstance(recordType), extraKeyFields, seed: 0);

            PopulateFields(recordA, keyFieldSubset, seed: 1);
            PopulateFields(recordB, keyFieldSubset, seed: 1);
            PopulateFields(recordC, keyFieldSubset, seed: 2);

            var equalsMethod = GetEqualsMethod(recordType, keyComparer);
            Assert.NotNull(equalsMethod);

            Assert.True((bool)equalsMethod.Invoke(keyComparer, [recordA, recordB]), "recordA should equal recordB");
            Assert.False((bool)equalsMethod.Invoke(keyComparer, [recordA, recordC]), "recordA should not equal recordC");
            Assert.False((bool)equalsMethod.Invoke(keyComparer, [recordB, recordC]), "recordB should not equal recordC");
        }

        [Theory]
        [MemberData(nameof(RecordTypeKeyFieldCombinationsSubsetsData))]
        public void CompareToImplementationMatchesKeyComparer(Type recordType, string[] keyFieldSubset)
        {
            var keyComparer = GetKeyComparer(recordType);
            var extraKeyFields = GetKeyFields(recordType).Except(keyFieldSubset).ToArray();
            var recordA = PopulateFields(Activator.CreateInstance(recordType), extraKeyFields, seed: 0);
            var recordB = PopulateFields(Activator.CreateInstance(recordType), extraKeyFields, seed: 0);

            PopulateFields(recordA, keyFieldSubset, seed: 1, setIdentity: true);
            PopulateFields(recordB, keyFieldSubset, seed: 2, setIdentity: true);

            var equalsMethod = GetEqualsMethod(recordType, keyComparer);
            Assert.NotNull(equalsMethod);
            var compareToMethod = GetCompareToMethod(recordType);
            Assert.NotNull(compareToMethod);

            Assert.False((bool)equalsMethod.Invoke(keyComparer, [recordA, recordB]), "recordA should not equal recordB");
            Assert.NotEqual(0, (int)compareToMethod.Invoke(recordA, [recordB]));
            Assert.NotEqual(0, (int)compareToMethod.Invoke(recordB, [recordA]));
        }

        [Theory]
        [MemberData(nameof(RecordTypeKeyFieldCombinationsSubsetsData))]
        public void CompareToImplementationIsCommutative(Type recordType, string[] keyFieldSubset)
        {
            var keyComparer = GetKeyComparer(recordType);
            var extraKeyFields = GetKeyFields(recordType).Except(keyFieldSubset).ToArray();
            var recordA = PopulateFields(Activator.CreateInstance(recordType), extraKeyFields, seed: 0);
            var recordB = PopulateFields(Activator.CreateInstance(recordType), extraKeyFields, seed: 0);

            PopulateFields(recordA, keyFieldSubset, seed: 1, setIdentity: true);
            PopulateFields(recordB, keyFieldSubset, seed: 2, setIdentity: true);

            var compareToMethod = GetCompareToMethod(recordType);
            Assert.NotNull(compareToMethod);

            Assert.NotEqual((int)compareToMethod.Invoke(recordB, [recordA]), (int)compareToMethod.Invoke(recordA, [recordB]));
        }

        [Theory]
        [MemberData(nameof(RecordTypeKeyFieldCombinationsSubsetsData))]
        public void CompareToImplementationIsStable(Type recordType, string[] keyFieldSubset)
        {
            var keyComparer = GetKeyComparer(recordType);
            var extraKeyFields = GetKeyFields(recordType).Except(keyFieldSubset).ToArray();
            var recordA = PopulateFields(Activator.CreateInstance(recordType), extraKeyFields, seed: 0);
            var recordB = PopulateFields(Activator.CreateInstance(recordType), extraKeyFields, seed: 0);
            var recordC = PopulateFields(Activator.CreateInstance(recordType), extraKeyFields, seed: 0);

            PopulateFields(recordA, keyFieldSubset, seed: 1, setIdentity: true);
            PopulateFields(recordB, keyFieldSubset, seed: 2, setIdentity: true);
            PopulateFields(recordC, keyFieldSubset, seed: 3, setIdentity: true);

            var compareToMethod = GetCompareToMethod(recordType);
            Assert.NotNull(compareToMethod);

            var cAB = Math.Clamp((int)compareToMethod.Invoke(recordA, [recordB]), -1, 1);
            var cBC = Math.Clamp((int)compareToMethod.Invoke(recordB, [recordC]), -1, 1);
            var cAC = Math.Clamp((int)compareToMethod.Invoke(recordA, [recordC]), -1, 1);
            Assert.NotEqual(0, cAB);
            Assert.NotEqual(0, cBC);
            Assert.NotEqual(0, cAC);

            object[] Sort(object[] records)
            {
                Array.Sort(records, (x, y) => (int)compareToMethod.Invoke(x, [y]));
                return records;
            }

            object[][] allSort = [
                Sort([recordA, recordB, recordC]),
                Sort([recordA, recordC, recordB]),
                Sort([recordB, recordA, recordC]),
                Sort([recordB, recordC, recordA]),
                Sort([recordC, recordA, recordB]),
                Sort([recordC, recordB, recordA]),
            ];

            for (var i = 1; i < allSort.Length; i++)
            {
                Assert.Same(allSort[0][0], allSort[i][0]);
                Assert.Same(allSort[0][1], allSort[i][1]);
                Assert.Same(allSort[0][2], allSort[i][2]);
            }
        }

        [Theory]
        [MemberData(nameof(RecordTypesData))]
        public void CatalogScanRecordKeyComparerDoesNotConsiderOtherFields(Type recordType)
        {
            var keyComparer = GetKeyComparer(recordType);
            var keyFields = GetKeyFields(recordType);
            var recordA = PopulateFields(Activator.CreateInstance(recordType), keyFields, seed: 0);
            var recordB = PopulateFields(Activator.CreateInstance(recordType), keyFields, seed: 0);

            if (recordType.GetProperty(nameof(PackageRecord.ScanId)) is not null)
            {
                PopulateFields(recordA, [nameof(PackageRecord.ScanId)], seed: 1);
                PopulateFields(recordB, [nameof(PackageRecord.ScanId)], seed: 2);
            }
            else if (recordType.GetProperty(nameof(CatalogLeafItemRecord.CommitTimestamp)) is not null)
            {
                PopulateFields(recordA, [nameof(CatalogLeafItemRecord.CommitTimestamp)], seed: 1);
                PopulateFields(recordB, [nameof(CatalogLeafItemRecord.CommitTimestamp)], seed: 1);
            }
            else if (recordType.GetProperty(nameof(PackageDownloadRecord.AsOfTimestamp)) is not null)
            {
                PopulateFields(recordA, [nameof(PackageDownloadRecord.AsOfTimestamp)], seed: 1);
                PopulateFields(recordB, [nameof(PackageDownloadRecord.AsOfTimestamp)], seed: 1);
            }
            else
            {
                throw new NotImplementedException("Could not set a non key field to a random value.");
            }

            var equalsMethod = GetEqualsMethod(recordType, keyComparer);
            Assert.NotNull(equalsMethod);

            Assert.True((bool)equalsMethod.Invoke(keyComparer, [recordA, recordB]), "recordA should equal recordB");
        }

        private static MethodInfo GetEqualsMethod(Type recordType, object keyComparer)
        {
            return keyComparer.GetType().GetMethod(nameof(IEqualityComparer<object>.Equals), [recordType, recordType]);
        }

        private static MethodInfo GetCompareToMethod(Type recordType)
        {
            return recordType.GetMethod(nameof(IComparable<object>.CompareTo), [recordType]);
        }

        private static object GetKeyComparer(Type recordType)
        {
            return recordType.GetProperty(nameof(ICsvRecord<CatalogLeafItemRecord>.KeyComparer)).GetValue(null);
        }

        private static IReadOnlyList<string> GetKeyFields(Type recordType)
        {
            return (IReadOnlyList<string>)recordType.GetProperty(nameof(ICsvRecord<CatalogLeafItemRecord>.KeyFields)).GetValue(null);
        }

        private static object PopulateFields(object record, IReadOnlyList<string> fields, int seed, bool setIdentity = false)
        {
            var random = new Random(seed);
            var added = new HashSet<string>();

            int RandomInt()
            {
                while (true)
                {
                    var next = random.Next();
                    if (added.Add(next.ToString(CultureInfo.InvariantCulture)))
                    {
                        return next;
                    }
                }
            }

            long RandomPositiveInt()
            {
                while (true)
                {
                    var next = Math.Abs(random.Next());
                    if (added.Add(next.ToString(CultureInfo.InvariantCulture)))
                    {
                        return next;
                    }
                }
            }

            byte[] RandomBytes(int count)
            {
                var buffer = new byte[count];
                while (true)
                {
                    random.NextBytes(buffer);
                    if (added.Add(buffer.ToBase64().ToString(CultureInfo.InvariantCulture)))
                    {
                        return buffer;
                    }
                }
            }

            var recordType = record.GetType();

            // populate all key fields
            foreach (var propertyName in fields)
            {
                var property = recordType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);
                Assert.NotNull(property);

                switch (property.PropertyType)
                {
                    case var t when t == typeof(string):
                        property.SetValue(record, RandomInt().ToString(CultureInfo.InvariantCulture));
                        break;
                    case var t when t == typeof(int?):
                        property.SetValue(record, RandomInt());
                        break;
                    case var t when t == typeof(Guid?):
                        property.SetValue(record, new Guid(RandomBytes(16)));
                        break;
                    case var t when t == typeof(DateTimeOffset):
                        property.SetValue(record, new DateTimeOffset(RandomPositiveInt(), TimeSpan.Zero));
                        break;
                    default:
                        throw new NotImplementedException("Could not generate value for type " + property.PropertyType.FullName);
                }
            }

            if (setIdentity)
            {
                // override identity properties, this allows PackageRecord.CompareTo to work as expected.
                var lowerId = RandomInt().ToString(CultureInfo.InvariantCulture).ToLowerInvariant();
                var normalizedVersion = $"{RandomInt()}.{RandomInt()}.{RandomInt()}";

                recordType.GetProperty(nameof(IPackageRecord.LowerId), typeof(string))?.SetValue(record, lowerId);
                recordType.GetProperty(nameof(IPackageRecord.Id), typeof(string))?.SetValue(record, lowerId);
                recordType.GetProperty(nameof(IPackageRecord.Version), typeof(string))?.SetValue(record, lowerId);
                recordType.GetProperty(nameof(IPackageRecord.Identity), typeof(string))?.SetValue(record, PackageRecordExtensions.GetIdentity(lowerId, normalizedVersion));
            }

            return record;
        }

        public static IEnumerable<object[]> RecordTypesData = NuGetInsightsWorkerLogicKustoDDL
            .TypeToDefaultTableName
            .Select(x => new object[] { x.Key });

        public static IEnumerable<object[]> RecordTypeKeyFieldCombinationsSubsetsData
        {
            get
            {
                foreach (var pair in NuGetInsightsWorkerLogicKustoDDL.TypeToDefaultTableName)
                {
                    var keyFields = GetKeyFields(pair.Key);
                    foreach (var keys in SubSetsOf(keyFields))
                    {
                        var keyArray = keys.ToArray();
                        if (keyArray.Length == 0)
                        {
                            continue;
                        }

                        yield return new object[] { pair.Key, keyArray };
                    }
                }
            }
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/999182
        /// </summary>
        public static IEnumerable<IEnumerable<T>> SubSetsOf<T>(IEnumerable<T> source)
        {
            if (!source.Any())
            {
                return Enumerable.Repeat(Enumerable.Empty<T>(), 1);
            }

            var element = source.Take(1);
            var haveNots = SubSetsOf(source.Skip(1));
            var haves = haveNots.Select(set => element.Concat(set));
            return haves.Concat(haveNots);
        }

        private class ObjectArrayComparer : IComparer<object[]>
        {
            public int Compare(object[] x, object[] y)
            {
                if (x.Length != y.Length)
                {
                    throw new ArgumentException($"Arrays have different lengths. Right: {x.Length}. Left: {y.Length}.");
                }

                for (var i = 0; i < Math.Min(x.Length, y.Length); i++)
                {
                    if (x[i] is null)
                    {
                        throw new ArgumentException($"Element {i} of the first array is null");
                    }

                    if (y[i] is null)
                    {
                        throw new ArgumentException($"Element {i} of the second array is null");
                    }

                    if (x[i].GetType() != y[i].GetType())
                    {
                        throw new ArgumentException($"Element {i} of the arrays have different types. Right: {x[i].GetType()}. Left: {y[i].GetType()}.");
                    }

                    var c = Comparer.DefaultInvariant.Compare(x[i], y[i]);
                    if (c != 0)
                    {
                        return c;
                    }
                }

                return 0;
            }
        }

        public static IEnumerable<object[]> CatalogScanRecordTypesData = NuGetInsightsWorkerLogicKustoDDL
            .TypeToDefaultTableName
            .Where(x => !KnownAuxiliaryFileRecordTypes.Contains(x.Key))
            .Select(x => new object[] { x.Key });

        public static IEnumerable<object[]> AuxiliaryFileRecordTypesData = KnownAuxiliaryFileRecordTypes
            .Select(x => new object[] { x });

        public CsvRecordContainersTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
