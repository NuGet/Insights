# PackageFileToCsv

This driver hashes every file in the .nupkg as well as the .nupkg itself. The produces CSV has the .nupkg entry hashes.
The hash of the .nupkg itself is stored for the [SymbolPackageArchiveToCsv](SymbolPackageArchiveToCsv.md) driver.

|                                    |                                                                                                                                                                                                                                                                                          |
| ---------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogScanDriverType` enum value | `PackageFileToCsv`                                                                                                                                                                                                                                                                       |
| Driver implementation              | [`PackageFileToCsvDriver`](../../src/Worker.Logic/Drivers/PackageFileToCsv/PackageFileToCsvDriver.cs)                                                                                                                                                                                    |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                                                                                                                   |
| Cursor dependencies                | [`LoadPackageArchive`](LoadPackageArchive.md): needs to know about the ZIP entries package                                                                                                                                                                                               |
| Components using driver output     | [`PackageArchiveToCsv`](PackageArchiveToCsv.md): uses the .nupkg hash saves to table storage<br />Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation                                                                                         |
| Persistent storage config          | Blob Storage:<br />`PackageFileContainerName`: contains CSVs for the [`PackageArchives`](../tables/PackageFiles.md) table<br /><br />Table Storage:<br />`PackageHashesTableName`: contains hashes of the .nupkg file, if it exists                                                      |
| Output CSV tables                  | [`PackageFiles`](../tables/PackageFiles.md)                                                                                                                                                                                                                                              |

## Algorithm

When the incoming leaf scan is new, the .nupkg is downloaded. Then, the .nupkg is opened as a ZIP archive and each file
entries is hashed. The hashes are stored in table storage. The results are written out as CSV records.

If the incoming leaf is not new or is stale, cached file entry hashes are used to produce the CSV records.
