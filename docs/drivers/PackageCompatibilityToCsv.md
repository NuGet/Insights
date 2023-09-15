# PackageCompatibilityToCsv

This driver attempts to calculate package framework (TFM) compatibility as closely as possible to how NuGet restore works. Although the data is useful for many purposes, it does not exactly match NuGet restore logic. For example, the concept of runtimes (RID) is not expressed but this has tangible impact on NuGet restore.

|                                    |                                                                                                                                                                                                  |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CatalogScanDriverType` enum value | `PackageCompatibilityToCsv`                                                                                                                                                                      |
| Driver implementation              | [`PackageCompatibilityToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageCompatibilityToCsv/PackageCompatibilityToCsvDriver.cs)                                                     |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                           |
| Cursor dependencies                | [`LoadPackageArchive`](LoadPackageArchive.md): needs the package file list<br />[`LoadPackageManifest`](LoadPackageManifest.md): needs the package .nuspec                                       |
| Components using driver output     | Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data           |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation |
| Persistent storage config          | Blob Storage:<br />`PackageCompatibilityContainerName`: contains CSVs for the [`PackageCompatibilities`](../tables/PackageCompatibilities.md) table                                              |
| Output CSV tables                  | [`PackageCompatibilities`](../tables/PackageCompatibilities.md)                                                                                                                                  |

## Algorithm

For each catalog leaf passed to the driver, multiple package compatibility definitions are explored. Over the years of NuGet's growth in the .NET ecosystem, there have been multiple definitions of package compatibility. This driver attempts to execute and record all of the results.

Both the file list and the .nuspec are needed for this driver because NuGet package restore actually considers some parts of the .nuspec in addition to the file list (a little known fact).

The `NuspecReader` compatibility notion is an ancient compatibility implementation implemented in NuGet client [`NuspecReader`](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/NuspecReader.cs) type. It serves no real purpose in NuGet restore.

The `NU1202` compatibility notion is a set of frameworks reported to the user when there is a compatibility failure in restore. This is a subset of the true set and is useful for a user's investigation of their own broken restore.

The `NuGetGallery` compatibility notion matches the asset frameworks calculated by NuGet.org for [the Frameworks tab](https://devblogs.microsoft.com/nuget/introducing-compatible-frameworks-on-nuget-org/) (dark blue TFMs, not the light blue computed TFMs) and [search-by-TFM](https://devblogs.microsoft.com/nuget/introducing-search-by-target-framework-on-nuget-org/).

The `NuGetGalleryEscaped` compatibility notion is the same as the `NuGetGallery` notion except is is based escaped file paths. The reason for tracking this is because some packages on NuGet.org had their compatibility information backfilled using an escaped file path, rather than an unescaped one. The actual difference between results in `NuGetGallery` and `NuGetGalleryEscaped` as well as which packages on NuGet.org use which notion has not been explored.

The `NuGetGallerySupported` compatibility notion takes the asset frameworks of `NuGetGallery` and expand them to a full set of computed frameworks.

All of the compatibility detections return consistent results for supported frameworks as new .NET and NuGet versions are released, except the `NuGetGallerySupported` category which is an expanded, calculated set of all supported frameworks generated from a base set in `NuGetGallery` column. As new versions of .NET are released (and therefore new TFMs like `net7.0` for .NET 7's release), this driver needs to be rerun on all packages. If this is not done, the values in `NuGetGallerySupported` will be a subset of the actual set. Automating this process is tracked by [NuGet/Insights#94](https://github.com/NuGet/Insights/issues/94).