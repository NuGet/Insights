# PackageAssemblyToCsv

This driver performs details analysis of .NET assemblies contained in NuGet packages. The whole NuGet package is downloaded in the process so any details available in an .NET assembly (or even native binary) could be discovered by this driver.

|                                    |                                                                                                                                                                                                                                                                       |
| ---------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogScanDriverType` enum value | `PackageAssemblyToCsv`                                                                                                                                                                                                                                                |
| Driver implementation              | [`PackageAssemblyToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageAssemblyToCsv/PackageAssemblyToCsvDriver.cs)                                                                                                                                         |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                                                                                                |
| Cursor dependencies                | [V3 package content](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource): this driver needs the .nupkg from the package content resource                                                                                                       |
| Components using driver output     | [`PackageArchiveToCsv`](PackageArchiveToCsv.md): needs the package hashes                                                                                                                                                                                             |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation                                                                      |
| Persistent storage config          | Blob Storage:<br />`PackageAssemblyContainerName`: contains CSVs for the [`PackageAssemblies`](../tables/PackageAssemblies.md) table<br /><br />Table Storage:<br />`PackageHashesTableName`: contains package hashes calculated while the .nupkg is fully downloaded |
| Output CSV tables                  | [`PackageAssemblies`](../tables/PackageAssemblies.md)                                                                                                                                                                                                                 |

## Algorithm

This package downloads the whole NuGet package even if there are no know assembly file extensions in the package. This is because this driver has a second responsibility of keeping track of hashes of the .nupkg (not to be confused with the NuGet signature content hash available in the [`PackageSignatures`](../tables/PackageSignatures.md) table).

Any ZIP file entry that has a file extension matching a known assembly extension (e.g. `.dll`, `.exe`) is analyzed using .NET's `System.Reflection.Metadata` API. Most of the analysis is focus on .NET assemblies, instead of native (unmanaged) code.

For each file with a known assembly extension, a CSV record is produced containing all assembly information that could be gathered.
