using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class CsvRecordContainersTest : BaseWorkerLogicIntegrationTest
    {
        public CsvRecordContainersTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public CsvResultStorageContainers Target => Host.Services.GetRequiredService<CsvResultStorageContainers>();

        [Fact]
        public void ReturnsUniqueTypePerContainer()
        {
            var containerNames = Target.GetContainerNames();
            var types = new HashSet<Type>();

            foreach (var containerName in containerNames)
            {
                var type = Target.GetRecordType(containerName);
                Assert.DoesNotContain(type, types);
                types.Add(type);
            }
        }

        [Fact]
        public void ReturnsUniqueKustoTableNamePerContainer()
        {
            var containerNames = Target.GetContainerNames();
            var kustoTableNames = new HashSet<string>();

            foreach (var containerName in containerNames)
            {
                var kustoTableName = Target.GetKustoTableName(containerName);
                Assert.DoesNotContain(kustoTableName, kustoTableNames);
                kustoTableNames.Add(kustoTableName);
            }
        }

        [Fact]
        public void ReturnsSomeContainerNames()
        {
            Assert.NotEmpty(Target.GetContainerNames());
        }

        [Fact]
        public void ContainerNameCountMatchesKustoDDLCounts()
        {
            Assert.Equal(KustoDDL.TypeToDDL.Count, Target.GetContainerNames().Count);
            Assert.Equal(KustoDDL.TypeToDefaultTableName.Count, Target.GetContainerNames().Count);
        }
    }
}
