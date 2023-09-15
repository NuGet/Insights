# PackageVersionToCsv

This driver looks at all versions available per package ID to determine the latest version by SemVer and extracts other version number information to CSV.

|                                    |                                                                                                                                                                                                  |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CatalogScanDriverType` enum value | `PackageVersionToCsv`                                                                                                                                                                            |
| Driver implementation              | [`PackageVersionToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageVersionToCsv/PackageVersionToCsvDriver.cs)                                                                       |
| Processing mode                    | process latest catalog leaf per package ID                                                                                                                                                       |
| Cursor dependencies                | [`LoadPackageVersion`](LoadPackageVersion.md): needs a list of all available package versions                                                                                                    |
| Components using driver output     | Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data           |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation |
| Persistent storage config          | Blob Storage:<br />`PackageVersionContainerName`: contains CSVs for the [`PackageVersions`](../tables/PackageVersions.md) table                                                                  |
| Output CSV tables                  | [`PackageVersions`](../tables/PackageVersions.md)                                                                                                                                                |

## Algorithm

This driver is unlike many other drivers because it only needs to process a single catalog leaf per package ID in a catalog scan. That is, if there are catalog leaves of different versions for the same package ID, only one of those leaves needs to be passed to the driver. Version-specific information is loaded into Azure Table Storage by the [`LoadPackageVersion`](LoadPackageVersion.md) driver so by the time this driver runs, it just needs to look at individual package IDs.

For each catalog leaf passed to the driver, only the package ID is considered. Whatever version the catalog leaf contains is irrelevant. The list of all versions for the package are loaded from table storage for this given package ID. With this list of package versions and "is listed" status (also available in table storage), the driver can determine the current latest package version for the package ID.

There are four definitions of latest on NuGet.org:

1. Including prerelease versions, including SemVer 1.0.0 and 2.0.0 packages
2. Excluding prerelease versions, including SemVer 1.0.0 and 2.0.0 packages
3. Including prerelease versions, including only SemVer 1.0.0 packages
4. Excluding prerelease versions, including only Semver 1.0.0 packages.

In addition to these latest version determinations, other version metadata is calculated like the major version integer, the overall SemVer order in the list of versions (not considering listed status), etc.

All of these details are written to a CSV record per package version.
