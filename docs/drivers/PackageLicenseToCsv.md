# PackageLicenseToCsv

This driver downloads the embedded license file and parses the license expression and maps the data to a CSV record.

|                                    |                                                                                                                                                                                                  |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CatalogScanDriverType` enum value | `PackageLicenseToCsv`                                                                                                                                                                            |
| Driver implementation              | [`PackageLicenseToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageLicenseToCsv/PackageLicenseToCsvDriver.cs)                                                                       |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                           |
| Cursor dependencies                | [V3 package content](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource): this driver needs the license file from the package content resource                            |
| Components using driver output     | Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data           |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation |
| Persistent storage config          | Blob Storage:<br />`PackageLicenseContainerName`: contains CSVs for the [`PackageLicenses`](../tables/PackageLicenses.md) table                                                                  |
| Output CSV tables                  | [`PackageLicenses`](../tables/PackageLicenses.md)                                                                                                                                                |

## Algorithm

For each catalog leaf passed to the driver, the license type is determined. This can either be a file (embedded license file), expression (SPDX license expression in package metadata), or a legacy license URL. A package may have no license information at all.

If the package has a license expression, the expression is parsed using NuGet client's [`NuGetLicenseExpression.Parse`](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/Licenses/NuGetLicenseExpression.cs) logic. The parsed expression is inspected and some helpful summaries are added to the CSV record.

If the package has a license file, the file is downloaded from the NuGet.org V3 package content resource. The license file bytes are read using .NET's `StreamReader` class which attempts to detect string encoding. The full license content is added to the CSV record.

This driver provides more details about the package license than what is available in the [`PackageManifests`](../tables/PackageManifests.md) table, produced by the [`PackageManifestToCsv`](PackageManifestToCsv.md) driver.
