// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        public CatalogScanDriverMetadataTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
