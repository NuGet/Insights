# PackageIconToCsv

This driver reads package icon files from NuGet.org and maps image metadata to CSV records.

|                                    |                                                                                                                                                                                                  |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CatalogScanDriverType` enum value | `PackageIconToCsv`                                                                                                                                                                               |
| Driver implementation              | [`PackageIconToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageIconToCsv/PackageIconToCsvDriver.cs)                                                                                |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                           |
| Cursor dependencies                | [V3 package content](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource): TODO SHORT DESCRIPTION                                                                          |
| Components using driver output     | Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data           |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation |
| Persistent storage config          | Blob Storage:<br />`PackageIconContainerName`: contains CSVs for the [`PackageIcons`](../tables/PackageIcons.md) table                                                                           |
| Output CSV tables                  | [`PackageIcons`](../tables/PackageIcons.md)                                                                                                                                                      |

## Algorithm

For each catalog leaf passed to the driver, the package icon is downloaded from the NuGet.org V3 package content resource. A package may not have an icon. The icon stored in the package content resource is either a cached copy of the icon specified by the package author in the legacy `iconUrl` package metadata field or a copy of the embedded icon file.

Once the icon file is downloaded, an attempt is made to detect the format code based off of [NuGet.org's own image format detection logic](../../src/Worker.Logic/CatalogScan/Drivers/PackageIconToCsv/FormatDetector.cs). 

Then [Magick.NET](https://github.com/dlemstra/Magick.NET) is used to perform rigorous analysis of the image file. Various properties are pulled from the Magick.NET results into a CSV record. Magick.NET is used because it can actually read various image format and determine things like image dimensions and [Exif](https://en.wikipedia.org/wiki/Exif) metadata.
