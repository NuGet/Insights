# PackageContentToCsv

This driver loads the full content of specific file extensions and writes them to CSV.

|                                    |                                                                                                                                                                                                  |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CatalogScanDriverType` enum value | `PackageContentToCsv`                                                                                                                                                                            |
| Driver implementation              | [`PackageContentToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageContentToCsv/PackageContentToCsvDriver.cs)                                                                       |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                           |
| Cursor dependencies                | [`LoadPackageArchive`](LoadPackageArchive.md): needs ZIP file listing to determine if a package should be downloaded                                                                             |
| Components using driver output     | Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data           |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation |
| Persistent storage config          | Blob Storage:<br />`PackageContentContainerName`: contains CSVs for the [`PackageContents`](../tables/PackageContents.md) table                                                                  |
| Output CSV tables                  | [`PackageContents`](../tables/PackageContents.md)                                                                                                                                                |

## Algorithm

For each catalog leaf passed into the driver, the file listing is fetched from Azure Table Storage (as stored by [`LoadPackageArchive`](LoadPackageArchive.md)). The file list is filtered and sorted based on the `PackageContentFileExtensions` configuration to consider only specific file extensions. If at least one file matches the list of extensions, the .nupkg is downloaded from the NuGet.org V3 package content resource. 

The filtered file entries are then sorted by file extension (preferring file extensions earlier in the `PackageContentFileExtensions` configuration), then preferring files recognized as NuGet assets, then preferring ZIP entries appearing earlier in the ZIP central directory. This preference order for the files to load into CSV is needed because there is a limit of `PackageContentMaxSizePerPackage` bytes per package. This is to prevent packages with a lot of files or some large files from bloating the output CSV files with too much data.

The sorted and filtered list of file entries is then processed in order. Each file entry is read from the downloaded ZIP archive and read as a string, using .NET's `StreamReader` and its built-in encoding detection. At most `PackageContentMaxSizePerFile` bytes are read into the content string. The entire file is buffered through a `CryptoStream` so an accurate file hash can still be captured.

The file content, hash, and other metadata is loaded into a CSV record per file entry.
