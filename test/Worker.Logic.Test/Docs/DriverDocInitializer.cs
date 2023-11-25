// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    internal class DriverDocInitializer
    {
        private const string None = "none";
        private readonly DriverDocInfo _info;

        public DriverDocInitializer(DriverDocInfo info)
        {
            _info = info;
        }

        private string GetDriverLink(CatalogScanDriverType driverType)
        {
            return $"[`{driverType}`]({driverType}.md)";
        }

        public string Build()
        {
            var builder = new StringBuilder();

            var processingMode = (_info.IndexResult, _info.PageResult) switch 
            {
                (CatalogIndexScanResult.ExpandAllLeaves, CatalogPageScanResult.Processed) => "process just the catalog page",
                (CatalogIndexScanResult.ExpandAllLeaves, CatalogPageScanResult.ExpandAllowDuplicates) => "process all catalog leaves, including duplicates",
                (CatalogIndexScanResult.ExpandLatestLeaves, null) => "process latest catalog leaf per package ID and version",
                (CatalogIndexScanResult.ExpandLatestLeavesPerId, null) => "process latest catalog leaf per package ID",
                _ => throw new NotImplementedException($"The combination of index result {_info.IndexResult} and page result {_info.PageResult} are not supported.")
            };

            var cursorDependencies = new List<string>();
            if (_info.DependsOnFlatContainer)
            {
                cursorDependencies.Add("[V3 package content](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource): TODO SHORT DESCRIPTION");
            }

            foreach (var dependency in _info.DriverDependencies)
            {
                cursorDependencies.Add(GetDriverLink(dependency) + ": TODO SHORT DESCRIPTION");
            }

            var dependents = new List<string>();
            foreach (var dependent in _info.DriverDependents)
            {
                dependents.Add(GetDriverLink(dependent) + ": TODO SHORT DESCRIPTION");
            }
            dependents.Add("TODO OTHER COMPONENTS");

            var intermediateContainerLines = GetStorageContainerLines(_info.IntermediateContainers);
            var addedContainerLines = GetStorageContainerLines(_info.PersistentContainers);

            var csvTables = new List<string>();
            foreach (var csvTable in _info.CsvTables)
            {
                csvTables.Add($"[`{csvTable}`](../tables/{csvTable}.md)");
            }
            if (csvTables.Count == 0)
            {
                csvTables.Add(None);
            }

            builder.AppendLine(CultureInfo.InvariantCulture, $"# {_info.DriverType}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"");
            builder.AppendLine(CultureInfo.InvariantCulture, $"TODO DRIVER DESCRIPTION");
            builder.AppendLine(CultureInfo.InvariantCulture, $"");
            builder.AppendLine(CultureInfo.InvariantCulture, $"|                                    |      |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| ---------------------------------- | ---- |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| `CatalogScanDriverType` enum value | `{_info.DriverType}` |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Driver implementation              | [`{_info.DriverType}Driver`](../../src/Worker.Logic/CatalogScan/Drivers/{_info.DriverType}/{_info.DriverType}Driver.cs) |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Processing mode                    | {processingMode} |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Cursor dependencies                | {string.Join("<br />", cursorDependencies)} |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Components using driver output     | {string.Join("<br />", dependents)} |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Temporary storage config           | {string.Join("<br />", intermediateContainerLines)} |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Persistent storage config          | {string.Join("<br />", addedContainerLines)} |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"| Output CSV tables                  | {string.Join("<br />", csvTables)} |");
            builder.AppendLine(CultureInfo.InvariantCulture, $"");
            builder.AppendLine(CultureInfo.InvariantCulture, $"## Algorithm");
            builder.AppendLine(CultureInfo.InvariantCulture, $"");
            builder.AppendLine(CultureInfo.InvariantCulture, $"TODO ALGORITHM DESCRIPTION");

            return builder.ToString();
        }

        private List<string> GetStorageContainerLines(HashSet<ConfiguredStorage> containers)
        {
            var storageContainerLines = new List<string>();
            StorageContainerType? lastType = null;
            foreach ((var type, var name, var prefix) in containers.OrderBy(x => x.Type).ThenBy(x => x.Name, StringComparer.Ordinal))
            {
                if (lastType != type)
                {
                    if (lastType.HasValue)
                    {
                        storageContainerLines.Add(string.Empty);
                    }

                    var typeString = type switch
                    {
                        StorageContainerType.Table => "Table Storage",
                        StorageContainerType.BlobContainer => "Blob Storage",
                        StorageContainerType.Queue => "Queue Storage",
                        _ => throw new NotImplementedException(),
                    };
                    storageContainerLines.Add($"{typeString}:");
                }

                storageContainerLines.Add($"`{name}`{(prefix ? " (name prefix)" : string.Empty)}: TODO STORAGE DESCRIPTION");
                lastType = type;
            }

            if (containers.Count == 0)
            {
                storageContainerLines.Add(None);
            }

            return storageContainerLines;
        }
    }
}
