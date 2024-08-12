# PackageAssemblyToCsv

This driver performs detailed analysis of .NET assemblies contained in NuGet packages. The whole NuGet package is downloaded in the process so any details available in an .NET assembly (or even native binary) could be discovered by this driver.

|                                    |                                                                                                                                                                                                  |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CatalogScanDriverType` enum value | `PackageAssemblyToCsv`                                                                                                                                                                           |
| Driver implementation              | [`PackageAssemblyToCsvDriver`](../../src/Worker.Logic/Drivers/PackageAssemblyToCsv/PackageAssemblyToCsvDriver.cs)                                                                                |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                           |
| Cursor dependencies                | [`LoadPackageArchive`](LoadPackageArchive.md): needs ZIP file listed to check for known extensions                                                                                               |
| Components using driver output     | Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data           |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation |
| Persistent storage config          | Blob Storage:<br />`PackageAssemblyContainerName`: contains CSVs for the [`PackageAssemblies`](../tables/PackageAssemblies.md) table                                                             |
| Output CSV tables                  | [`PackageAssemblies`](../tables/PackageAssemblies.md)                                                                                                                                            |

## Algorithm

Any ZIP that contains a known assembly extension (e.g. `.dll`, `.exe`) is fully downloaded. This cached ZIP metadata is used to determine whether the full ZIP should be downloaded for further analysis. Any ZIP file entry with an assembly extension is analyzed using .NET's `System.Reflection.Metadata` API. Most of the analysis is focus on .NET assemblies, instead of native (unmanaged) code.

For each file with a known assembly extension, a CSV record is produced containing all assembly information that could be gathered.
