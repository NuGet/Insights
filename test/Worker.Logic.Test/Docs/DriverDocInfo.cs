// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Markdig.Extensions.Tables;

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
            DriverDependencies = CatalogScanDriverMetadata.GetDependencies(driverType).OrderBy(x => x.ToString()).ToList();
            DriverDependents = CatalogScanDriverMetadata.GetDependents(driverType).OrderBy(x => x.ToString()).ToList();
            IntermediateContainers = intermediateContainers;
            PersistentContainers = persistentContainers;
            CsvTables = csvTables;
        }

        public CatalogScanDriverType DriverType { get; }
        public CatalogIndexScanResult IndexResult { get; }
        public CatalogPageScanResult? PageResult { get; }
        public IReadOnlyList<CatalogScanDriverType> DriverDependencies { get; }
        public IReadOnlyList<CatalogScanDriverType> DriverDependents { get; }
        public HashSet<ConfiguredStorage> IntermediateContainers { get; }
        public HashSet<ConfiguredStorage> PersistentContainers { get; }
        public IReadOnlyList<string> CsvTables { get; }

        public override void ReadMarkdown()
        {
            if (!File.Exists(DocPath) || TestLevers.OverwriteDriverDocs)
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

        public List<TableRow> GetFirstTableRows()
        {
            ReadMarkdown();

            var table = MarkdownDocument.OfType<Table>().FirstOrDefault();
            Assert.NotNull(table);

            return table.Cast<TableRow>().ToList();
        }
    }
}
