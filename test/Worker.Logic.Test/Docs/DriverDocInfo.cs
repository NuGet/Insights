// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet.Insights.Worker
{
    public record ConfiguredStorage(StorageContainerType Type, string Name, bool Prefix);

    public class DriverDocInfo : DocInfo
    {
        public DriverDocInfo(
            CatalogScanDriverType driverType,
            CatalogIndexScanResult indexResult,
            CatalogPageScanResult? pageResult,
            HashSet<ConfiguredStorage> intermediateContainers,
            HashSet<ConfiguredStorage> persistentContainers,
            IReadOnlyList<string> csvTables)
            : base(Path.Combine("drivers", $"{driverType}.md"))
        {
            DriverType = driverType;
            IndexResult = indexResult;
            PageResult = pageResult;
            DependsOnFlatContainer = CatalogScanDriverMetadata.GetFlatContainerDependents().Contains(driverType);
            DriverDependencies = CatalogScanDriverMetadata.GetDependencies(driverType);
            DriverDependents = CatalogScanDriverMetadata.GetDependents(driverType);
            IntermediateContainers = intermediateContainers;
            PersistentContainers = persistentContainers;
            CsvTables = csvTables;
        }

        public CatalogScanDriverType DriverType { get; }
        public CatalogIndexScanResult IndexResult { get; }
        public CatalogPageScanResult? PageResult { get; }
        public bool DependsOnFlatContainer { get; }
        public IReadOnlyList<CatalogScanDriverType> DriverDependencies { get; }
        public IReadOnlyList<CatalogScanDriverType> DriverDependents { get; }
        public HashSet<ConfiguredStorage> IntermediateContainers { get; }
        public HashSet<ConfiguredStorage> PersistentContainers { get; }
        public IReadOnlyList<string> CsvTables { get; }

        public override void ReadMarkdown()
        {
            if (!File.Exists(DocPath) || DriverDocsTest.OverwriteDriverDocs)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DocPath));

                var initializer = new DriverDocInitializer(this);
                using (var file = new FileStream(DocPath, FileMode.Create, FileAccess.Write))
                using (var writer = new StreamWriter(file))
                {
                    writer.Write(initializer.Build());
                }
            }

            base.ReadMarkdown();
        }
    }
}
