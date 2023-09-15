# PackageArchiveToCsv

This driver maps ZIP archive details to CSV about each .nupkg (NuGet package) on NuGet.org. It writes archive-level information as well as ZIP entry (file) level information.

|                                    |                                                                                                                                                                                                                                                                    |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CatalogScanDriverType` enum value | `PackageArchiveToCsv`                                                                                                                                                                                                                                              |
| Driver implementation              | [`PackageArchiveToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageArchiveToCsv/PackageArchiveToCsvDriver.cs)                                                                                                                                         |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                                                                                             |
| Cursor dependencies                | [`LoadPackageArchive`](LoadPackageArchive.md): reads ZIP structure from table storage<br />[`PackageAssemblyToCsv`](PackageAssemblyToCsv.md): saves archive hashes to table storage                                                                                |
| Components using driver output     | Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data                                                                             |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation                                                                   |
| Persistent storage config          | Blob Storage:<br />`PackageArchiveContainerName`: contains CSVs for the [`PackageArchives`](../tables/PackageArchives.md) table<br />`PackageArchiveEntryContainerName`: contains CSVs for the [`PackageArchiveEntries`](../tables/PackageArchiveEntries.md) table |
| Output CSV tables                  | [`PackageArchiveEntries`](../tables/PackageArchiveEntries.md)<br />[`PackageArchives`](../tables/PackageArchives.md)                                                                                                                                               |

## Algorithm

For each catalog leaf passed to driver, the ZIP central directory, size, and HTTP response headers are fetched from Azure Table Storage. These are populated by the [`LoadPackageArchive`](LoadPackageArchive.md) driver. Hashes of the whole ZIP are also read from table storage. These are populated by the [`PackageAssemblyToCsv`](PackageAssemblyToCsv.md) driver (because it needs the full ZIP content).

The ZIP central directory is enumerated. A single CSV record is produced for each .nupkg and one or more CSV records are created for each entry in the ZIP file.

Detailed ZIP information is included in the produced CSV records to aid in the debugging of esoteric ZIP archive issues.
