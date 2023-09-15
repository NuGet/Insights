# LoadPackageReadme

This driver loads package README content into Azure Table Storage for other drivers to use. This is an optimization to reduce the amount of data downloads from NuGet.org's public APIs in later steps.

A package README is an optional part of a package. It is considered a Markdown document. Some package READMEs are part of the package (embedded README) and others are provided to NuGet.org during the upload flow or later after the package is published.

|                                    |                                                                                                                                                                                          |
| ---------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogScanDriverType` enum value | `LoadPackageReadme`                                                                                                                                                                      |
| Driver implementation              | [`LoadPackageReadmeDriver`](../../src/Worker.Logic/CatalogScan/Drivers/LoadPackageReadme/LoadPackageReadmeDriver.cs)                                                                     |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                   |
| Cursor dependencies                | [V3 package content](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource): this driver needs embedded README from the package content resource                     |
| Components using driver output     | [`PackageReadmeToCsv`](PackageReadmeToCsv.md): uses the stored README for projecting to CSV                                                                                              |
| Temporary storage config           | none                                                                                                                                                                                     |
| Persistent storage config          | Table Storage:<br />`PackageReadmeTableName`: README bytes and metadata are stored using MessagePack and [`WideEntityStorageService`](../../src/Logic/WideEntities/WideEntityService.cs) |
| Output CSV tables                  | none                                                                                                                                                                                     |

## Algorithm

A batch of catalog leaf items are passed to the driver. For each catalog leaf, `HttpClient` is used to fetch the README from the first of two possible places: a configured non-embedded README location (using `LegacyReadmeUrlPattern` in configuration) or from the V3 package content resource. Not all packages have a README.

Both the legacy (non-embedded) README location and the embedded README location are attempted in this driver based on the catalog. This is actually not a perfect solution. NuGet.org supports updating legacy READMEs at any time (and multiple times) after the package is published. These legacy README updates don't result in catalog leaf items being produced, which means that the legacy README available on NuGet.org may not be seen by NuGet Insights. The only way to resolve this is for NuGet.org to somehow signal a legacy README update or by periodically checking the legacy README location. This latter idea is tracked by [NuGet/Insights#67](https://github.com/NuGet/Insights/issues/67).
