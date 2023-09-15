# LoadPackageVersion

This driver loads package README content into Azure Table Storage for other drivers to use. This is an optimization to allow other drivers to easily discover all package versions that are available for a package ID.

|                                    |                                                                                                                                                    |
| ---------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogScanDriverType` enum value | `LoadPackageVersion`                                                                                                                               |
| Driver implementation              | [`LoadPackageVersionDriver`](../../src/Worker.Logic/CatalogScan/Drivers/LoadPackageVersion/LoadPackageVersionDriver.cs)                            |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                             |
| Cursor dependencies                | [V3 package content](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource): blocks on this cursor to align with other drivers |
| Components using driver output     | [`PackageVersionToCsv`](PackageVersionToCsv.md): needs the full version list to determine latest                                                   |
| Temporary storage config           | none                                                                                                                                               |
| Persistent storage config          | Table Storage:<br />`PackageVersionTableName`: one record per package version, partitioned by package ID                                           |
| Output CSV tables                  | none                                                                                                                                               |

## Algorithm

This driver stores one row in Azure Table Storage per package version. The partition key for the records is the lowercase package ID. The row key for the records is the normalized, lowercase package ID. Other metadata is included in the row such as "is listed" and SemVer version information which is needed for determining the latest version package ID.

On NuGet.org, there are four parallel definitions of the latest package version. None of them are based on a timestamp (i.e. none are chronological order). The latest definitions consider two boolean options of including prerelease versions and including SemVer 2.0.0 versions. Because of this, a consumer of this version list information that is interested in latest determination would need to know the listed and SemVer 2.0.0 status of each version.
