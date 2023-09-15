# LoadLatestPackageLeaf

This driver records the latest catalog leaf document per package ID and version. The catalog can have many documents related to a given package ID and version so it's convenient to have an index to find the latest relevant document.

|                                    |                                                                                                                                                                                                                                                   |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogScanDriverType` enum value | `LoadLatestPackageLeaf`                                                                                                                                                                                                                           |
| Driver implementation              | Generic [`FindLatestLeafDriver`](../../src/Worker.Logic/CatalogScan/LatestLeaf/FindLatestLeafDriver.cs) with a [`LatestPackageLeafStorage`](../../src/Worker.Logic/CatalogScan/Drivers/LoadLatestPackageLeaf/LatestPackageLeafStorage.cs) adapter |
| Processing mode                    | process just the catalog page                                                                                                                                                                                                                     |
| Cursor dependencies                | [V3 package content](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource): blocks on this cursor to align with other drivers                                                                                                |
| Components using driver output     | none                                                                                                                                                                                                                                              |
| Temporary storage config           | none                                                                                                                                                                                                                                              |
| Persistent storage config          | Table Storage:<br />`LatestPackageLeafTableName`: indexable by package ID and version, contains a pointer to the latest catalog leaf                                                                                                              |
| Output CSV tables                  | none                                                                                                                                                                                                                                              |

## Algorithm

This driver's output is not used by any other component currently. It serves as the most simple implementation of the distributed `FindLatestCatalogLeaf` scan that is needed for this project for several purposes. Changes to this distributed scan can be tested easily using this driver since the output is easy to understand (simple Azure Table Storage schema).

The `FindLatestCatalogLeaf` scan sees each catalog page and writes an Azure Table Storage record containing ID, version, commit timestamp, and other data. The partition key and row key are package ID and version respectively so if the record already exists, it is only updated if the commit timestamp of the current catalog leaf is later than the one in storage. After this is complete for all catalog pages that are in the catalog scan commit timestamp range, an Azure Queue Storage message is enqueued for each table row.

Each queue message performs an arbitrary action per row in the table. When the action is complete, the row is deleted. At the end of the process, the temporary table should be empty, signalling that the scan is complete.

Instead of just operating on the catalog directly, reduces the amount of duplicate processing that is done per package ID and version, because duplicates in the catalog are eliminated during the first step (when each catalog leaf is written to an "ID + version" record in the table).

This `LoadLatestPackageLeaf` driver skips the enqueueing step and simply retains the Azure Table Storage records indefinitely.
