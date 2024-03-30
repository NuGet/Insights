// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.DownloadsToCsv;
using NuGet.Insights.Worker.ExcludedPackagesToCsv;
using NuGet.Insights.Worker.OwnersToCsv;
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

        public static IEnumerable<object[]> RecordTypesData = KustoDDL
            .TypeToDefaultTableName
            .Select(x => new object[] { x.Key });

        public CsvRecordContainersTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
