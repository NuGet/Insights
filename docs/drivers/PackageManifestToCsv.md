# PackageManifestToCsv

This driver parsed each NuGet package .nuspec file (the package manifest) and maps well known properties to CSV.

|                                    |                                                                                                                                                                                                  |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CatalogScanDriverType` enum value | `PackageManifestToCsv`                                                                                                                                                                           |
| Driver implementation              | [`PackageManifestToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageManifestToCsv/PackageManifestToCsvDriver.cs)                                                                    |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                           |
| Cursor dependencies                | [`LoadPackageManifest`](LoadPackageManifest.md): reads .nuspec bytes from table storage                                                                                                          |
| Components using driver output     | Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data           |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation |
| Persistent storage config          | Blob Storage:<br />`PackageManifestContainerName`: contains CSVs for the [`PackageManifests`](../tables/PackageManifests.md) table                                                               |
| Output CSV tables                  | [`PackageManifests`](../tables/PackageManifests.md)                                                                                                                                              |

## Algorithm

For each catalog leaf passed to the driver, the package manifest (i.e. the .nuspec file) is fetched from Azure Table Storage. These are populated by the [`LoadPackageManifest`](LoadPackageManifest.md) driver. The .nuspec bytes are loaded as a `NuspecReader` which is used to find well known .nuspec properties.

Simple scalar properties are stored as-is in a CSV record. Complex properties (arrays or objects) are serialized to JSON to be treated as `dynamic` data in Kusto.
