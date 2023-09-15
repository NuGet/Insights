# PackageAssetToCsv

This driver analyzed every NuGet package on NuGet.org using the same logic that is used by the NuGet restore operation. The goal of this analysis is to see NuGet packages in the way that NuGet restore does: what assets are available in the package and which frameworks are they applicable to.

This was the first driver that was implemented. It proved that running deep analysis of NuGet packages on NuGet.org could show interesting business trends (e.g. .NET Core uptake).

|                                    |                                                                                                                                                                                                  |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CatalogScanDriverType` enum value | `PackageAssetToCsv`                                                                                                                                                                              |
| Driver implementation              | [`PackageAssetToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageAssetToCsv/PackageAssetToCsvDriver.cs)                                                                             |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                           |
| Cursor dependencies                | [`LoadPackageArchive`](LoadPackageArchive.md): needs ZIP file listing                                                                                                                            |
| Components using driver output     | Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data           |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation |
| Persistent storage config          | Blob Storage:<br />`PackageAssetContainerName`: contains CSVs for the [`PackageAssets`](../tables/PackageAssets.md) table                                                                        |
| Output CSV tables                  | [`PackageAssets`](../tables/PackageAssets.md)                                                                                                                                                    |

## Algorithm

For each catalog leaf passed to the driver, the ZIP directory is fetched from Azure Table Storage (as stored by [`LoadPackageArchive`](LoadPackageArchive.md)). From this ZIP directory a full list of files in the package is built. This list of files is passed to NuGet client APIs like [`ManagedCodeConventions`](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/ContentModel/ManagedCodeConventions.cs). The various file path pattern sets used by NuGet restore are run against the file list to build out a list of package assets and their associated properties (such as code language for content files or target framework for runtime assemblies).

A CSV record is produced for each recognized package asset. A file can be considered by multiple asset patterns so a single file path may appear multiple times in the table.

THe target frameworks discovered by this analysis are a close approximation for package compatibility but are not exactly the same. To understand package compatibility, it's better to use the [`PackageCompatibilities`](../tables/PackageCompatibilities.md) which goes to greater lengths to mimic the many complexities of package compatibility.