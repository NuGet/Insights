# LoadSymbolPackageArchive

This driver uses [MiniZip](https://www.nuget.org/packages/Knapcode.MiniZip) to read the ZIP directory information for each .snupkg. This information is stored in Azure Table Storage for other drivers to use. This is an optimization to reduce the amount of data downloads from NuGet.org's APIs in later steps.

|                                    |                                                                                                                                                                                       |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogScanDriverType` enum value | `LoadSymbolPackageArchive`                                                                                                                                                            |
| Driver implementation              | [`LoadSymbolPackageArchiveDriver`](../../src/Worker.Logic/CatalogScan/Drivers/LoadSymbolPackageArchive/LoadSymbolPackageArchiveDriver.cs)                                             |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                |
| Cursor dependencies                | [V3 package content](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource): blocks on this cursor to align with other drivers                                    |
| Components using driver output     | [`SymbolPackageArchiveToCsv`](SymbolPackageArchiveToCsv.md): maps ZIP archive entry metadata to CSV                                                                                   |
| Temporary storage config           | none                                                                                                                                                                                  |
| Persistent storage config          | Table Storage:<br />`SymbolPackageArchiveTableName`: ZIP directory bytes stored using MessagePack and [`WideEntityStorageService`](../../src/Logic/WideEntities/WideEntityService.cs) |
| Output CSV tables                  | none                                                                                                                                                                                  |

## Algorithm

A batch of catalog leaf items are passed to the driver. For each catalog leaf, MiniZip is used to fetch just the symbol package (.snupkg) ZIP directory data. This is done via minimal HTTP HEAD and GET `Range` requests so that the whole package is not downloaded. This is similar to the [`LoadPackageArchive`](LoadPackageArchive.md) driver but for the .snupkg instead of the .nupkg. There is no signature file expected in the .snupkg so that is not fetched.

The ZIP directory are serialized and compressed using MessagePack and then written into Azure Table Storage as "wide entities".

The `SymbolPackagesContainerBaseUrl` is used to define where to fetch .snupkg files from. This cannot be automatically fetched from the V3 package content resource because there is no defined egress flow for .snupkg files on NuGet.org. The way that symbol data is served from NuGet.org is via the [documented symbol server](https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg#nugetorg-symbol-server). This serves individual PDB files, not .snupkg archives.

The symbol package container base URL is attempted in this driver based on the catalog. This is actually not a perfect solution. NuGet.org supports updating symbol packages at any time (and multiple times) after the package is published. These symbol package updates don't result in catalog leaf items being produced, which means that symbol package available on NuGet.org may not be seen by NuGet Insights. The only way to resolve this is for NuGet.org to somehow signal a symbol package update or by periodically checking the symbol package location. This latter idea is tracked by [NuGet/Insights#67](https://github.com/NuGet/Insights/issues/67).
