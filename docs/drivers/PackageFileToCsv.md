# PackageFileToCsv

TODO DRIVER DESCRIPTION

|                                    |                                                                                                                                                                                                                                                                                                |
| ---------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogScanDriverType` enum value | `PackageFileToCsv`                                                                                                                                                                                                                                                                             |
| Driver implementation              | [`PackageFileToCsvDriver`](../../src/Worker.Logic/Drivers/PackageFileToCsv/PackageFileToCsvDriver.cs)                                                                                                                                                                                          |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                                                                                                                         |
| Cursor dependencies                | [V3 package content](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource):  TODO SHORT DESCRIPTION                                                                                                                                                                       |
| Components using driver output     | [`PackageArchiveToCsv`](PackageArchiveToCsv.md): TODO SHORT DESCRIPTION<br />TODO OTHER COMPONENTS<br />Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): TODO STORAGE DESCRIPTION<br />`TaskStateTableName` (name prefix): TODO STORAGE DESCRIPTION                                                                                                                                             |
| Persistent storage config          | Blob Storage:<br />`PackageFileContainerName`: TODO STORAGE DESCRIPTION<br /><br />Table Storage:<br />`PackageHashesTableName`: TODO STORAGE DESCRIPTION                                                                                                                                      |
| Output CSV tables                  | [`PackageFiles`](../tables/PackageFiles.md)                                                                                                                                                                                                                                                    |

## Algorithm

TODO ALGORITHM DESCRIPTION
f