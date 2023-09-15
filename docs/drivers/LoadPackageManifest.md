# LoadPackageManifest

This driver reads the .nuspec for each package on NuGet.org. This information is stored in Azure Table Storage for other drivers to use. This is an optimization to reduce the amount of data downloads from NuGet.org's public APIs in later steps.

|                                    |                                                                                                                                                                                                                              |
| ---------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogScanDriverType` enum value | `LoadPackageManifest`                                                                                                                                                                                                        |
| Driver implementation              | [`LoadPackageManifestDriver`](../../src/Worker.Logic/CatalogScan/Drivers/LoadPackageManifest/LoadPackageManifestDriver.cs)                                                                                                   |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                                                       |
| Cursor dependencies                | [V3 package content](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource): this driver needs the .nuspec from the package content resource                                                             |
| Components using driver output     | [`PackageManifestToCsv`](PackageManifestToCsv.md): .nuspec metadata fields written to CSV<br />[`PackageCompatibilityToCsv`](PackageCompatibilityToCsv.md): uses .nuspec and other information for compatibility calculation |
| Temporary storage config           | none                                                                                                                                                                                                                         |
| Persistent storage config          | Table Storage:<br />`PackageManifestTableName`: .nuspec bytes stored using MessagePack and [`WideEntityStorageService`](../../src/Logic/WideEntities/WideEntityService.cs)                                                   |
| Output CSV tables                  | none                                                                                                                                                                                                                         |

## Algorithm

A batch of catalog leaf items are passed to the driver. For each catalog leaf, `HttpClient` is used to fetch just the .nuspec from the NuGet V3 package content resource. The .nuspec bytes as provided by the package content resource are stored in Azure Table Storage as a wide entity.

The .nuspec file is the only required part of a NuGet package. It is an XML document and is sometimes referred to as the manifest.

