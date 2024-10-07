// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.CatalogDataToCsv;
using NuGet.Insights.Worker.DownloadsToCsv;
using NuGet.Insights.Worker.ExcludedPackagesToCsv;
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
            Assert.Equal(KustoDDL.TypeToDDL.Count, CsvRecordContainers.ContainerNames.Count);
            Assert.Equal(KustoDDL.TypeToDefaultTableName.Count, CsvRecordContainers.ContainerNames.Count);
            Assert.Equal(CsvRecordContainers.RecordTypes.Count, CsvRecordContainers.ContainerNames.Count);
        }

        [Theory]
        [MemberData(nameof(AuxiliaryFileRecordTypesData))]
        public void AuxiliaryRecordTypesAreIAggregatedRecord(Type recordType)
        {
            var producer = CsvRecordContainers.GetProducer(recordType);

            Assert.Equal(CsvRecordProducerType.AuxiliaryFileUpdater, producer.Type);
            Assert.Throws<ArgumentException>(() => recordType.IsAssignableTo(typeof(IAggregatedCsvRecord<>).MakeGenericType(recordType)));
        }

        [Theory]
        [MemberData(nameof(CatalogScanRecordTypesData))]
        public void CatalogScanRecordTypesAreIAggregatedRecord(Type recordType)
        {
            var producer = CsvRecordContainers.GetProducer(recordType);

            Assert.Equal(CsvRecordProducerType.CatalogScanDriver, producer.Type);
            Assert.True(recordType.IsAssignableTo(typeof(IAggregatedCsvRecord<>).MakeGenericType(recordType)));
        }

        [Theory]
        [MemberData(nameof(CatalogScanRecordTypesData))]
        public void CatalogScanRecordIsUniquelyIdentifiedByKeyFields(Type recordType)
        {
            var keyComparer = GetKeyComparer(recordType);
            var keyFields = GetKeyFields(recordType);
            var recordA = PopulateFields(Activator.CreateInstance(recordType), keyFields, seed: 0);
            var recordB = PopulateFields(Activator.CreateInstance(recordType), keyFields, seed: 0);
            var recordC = PopulateFields(Activator.CreateInstance(recordType), keyFields, seed: 1);

            var equalsMethod = GetEqualsMethod(recordType, keyComparer);
            Assert.NotNull(equalsMethod);

            Assert.Equal(true, equalsMethod.Invoke(keyComparer, [recordA, recordB]));
            Assert.Equal(false, equalsMethod.Invoke(keyComparer, [recordA, recordC]));
            Assert.Equal(false, equalsMethod.Invoke(keyComparer, [recordB, recordC]));
        }

        [Theory]
        [MemberData(nameof(CatalogScanRecordTypesData))]
        public void CatalogScanRecordIsKeyComparerDoesNotConsiderOtherFields(Type recordType)
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
            else
            {
                throw new NotImplementedException("Could not set a non key field to a random value.");
            }

            var equalsMethod = GetEqualsMethod(recordType, keyComparer);
            Assert.NotNull(equalsMethod);

            Assert.Equal(true, equalsMethod.Invoke(keyComparer, [recordA, recordB]));
        }

        private static MethodInfo GetEqualsMethod(Type recordType, object keyComparer)
        {
            return keyComparer.GetType().GetMethod(nameof(IEqualityComparer<object>.Equals), [recordType, recordType]);
        }

        private static object GetKeyComparer(Type recordType)
        {
            return recordType.GetProperty(nameof(IAggregatedCsvRecord<CatalogLeafItemRecord>.KeyComparer)).GetValue(null);
        }

        private static IReadOnlyList<string> GetKeyFields(Type recordType)
        {
            return (IReadOnlyList<string>)recordType.GetProperty(nameof(IAggregatedCsvRecord<CatalogLeafItemRecord>.KeyFields)).GetValue(null);
        }

        private static object PopulateFields(object record, IReadOnlyList<string> fields, int seed)
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

            // populate all key fields
            foreach (var propertyName in fields)
            {
                var property = record.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);
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

            return record;
        }

        public static IEnumerable<object[]> RecordTypesData = KustoDDL
            .TypeToDefaultTableName
            .Select(x => new object[] { x.Key });

        public static IEnumerable<object[]> CatalogScanRecordTypesData = KustoDDL
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
