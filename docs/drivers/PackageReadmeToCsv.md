# PackageReadmeToCsv

This driver reads package README markdown and writes the full content to CSV.

|                                    |                                                                                                                                                                                                  |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CatalogScanDriverType` enum value | `PackageReadmeToCsv`                                                                                                                                                                             |
| Driver implementation              | [`PackageReadmeToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageReadmeToCsv/PackageReadmeToCsvDriver.cs)                                                                          |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                           |
| Cursor dependencies                | [`LoadPackageReadme`](LoadPackageReadme.md): reads README bytes from table storage                                                                                                               |
| Components using driver output     | Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data           |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation |
| Persistent storage config          | Blob Storage:<br />`PackageReadmeContainerName`: contains CSVs for the [`PackageReadmes`](../tables/PackageReadmes.md) table                                                                     |
| Output CSV tables                  | [`PackageReadmes`](../tables/PackageReadmes.md)                                                                                                                                                  |

## Algorithm

For each catalog leaf passed to the driver, the package README bytes are fetched from Azure Table Storage (as stored by [`LoadPackageReadme`](LoadPackageReadme.md)). Not all packages have READMEs. This driver has the same caveats as the [`LoadPackageReadme`](LoadPackageReadme.md) driver in that legacy (non-embedded) README content may not match the latest content on NuGet.org.

The full README string is read from bytes using .NET's `StreamReader` (which attempts string encoding detection) amd is added to the CSV as an unrendered markdown string.
