# SymbolPackageFileToCsv

This driver hashes every file in the .snupkg as well as the .snupkg itself. These entries are mostly portable PDB files.
The produces CSV has the .snupkg entry hashes. The hash of the .snupkg itself is stored for the
[SymbolPackageArchiveToCsv](SymbolPackageArchiveToCsv.md) driver.

|                                    |                                                                                                                                                                                                                                                                                                       |
| ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogScanDriverType` enum value | `SymbolPackageFileToCsv`                                                                                                                                                                                                                                                                              |
| Driver implementation              | [`SymbolPackageFileToCsvDriver`](../../src/Worker.Logic/Drivers/SymbolPackageFileToCsv/SymbolPackageFileToCsvDriver.cs)                                                                                                                                                                               |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                                                                                                                                |
| Cursor dependencies                | [`LoadSymbolPackageArchive`](LoadSymbolPackageArchive.md): needs to know if a .snupkg exists for the package                                                                                                                                                                                          |
| Components using driver output     | [`SymbolPackageArchiveToCsv`](SymbolPackageArchiveToCsv.md): uses the .snupkg hash saves to table storage<br />Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation                                                                                                      |
| Persistent storage config          | Blob Storage:<br />`SymbolPackageFileContainerName`: contains CSVs for the [`PackageArchives`](../tables/SymbolPackageFiles.md) table<br /><br />Table Storage:<br />`SymbolPackageHashesTableName`: contains hashes of the .snupkg file, if it exists                                                |
| Output CSV tables                  | [`SymbolPackageFiles`](../tables/SymbolPackageFiles.md)                                                                                                                                                                                                                                               |

## Algorithm

When the incoming leaf scan is new, the .snupkg is downloaded. Then, the .snupkg is opened as a ZIP archive and each
file entries is hashed. The hashes are stored in table storage. The results are written out as CSV records.

If the incoming leaf is not new or is stale, cached file entry hashes are used to produce the CSV records.
