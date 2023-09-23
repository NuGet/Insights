# LoadPackageArchive

This driver uses [MiniZip](https://www.nuget.org/packages/Knapcode.MiniZip) to read the ZIP directory information for each .nupkg on NuGet.org as well as the package signature. This information is stored in Azure Table Storage for other drivers to use. This is an optimization to reduce the amount of data downloads from NuGet.org's public APIs in later steps.

If a driver does not need actual package content and only needs the ZIP file listing (relatively small data), it can use the [`PackageFileService`](../../src/Logic/Storage/PackageFileService.cs) which reads the data produced by this driver.

|                                    |                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| ---------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CatalogScanDriverType` enum value | `LoadPackageArchive`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| Driver implementation              | [`LoadPackageArchiveDriver`](../../src/Worker.Logic/CatalogScan/Drivers/LoadPackageArchive/LoadPackageArchiveDriver.cs)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| Cursor dependencies                | [V3 package content](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource): this driver needs the .nupkg from the package content resource                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| Components using driver output     | [`PackageArchiveToCsv`](PackageArchiveToCsv.md): maps ZIP archive entry metadata to CSV<br />[`PackageAssetToCsv`](PackageAssetToCsv.md): uses ZIP file listing to determine pacakge assets<br />[`PackageCertificateToCsv`](PackageCertificateToCsv.md): needs the package signature<br />[`PackageSignatureToCsv`](PackageSignatureToCsv.md): needs the package signature<br />[`PackageCompatibilityToCsv`](PackageCompatibilityToCsv.md): needs the ZIP file listing for compatibility computation<br />[`PackageContentToCsv`](PackageContentToCsv.md): needs the ZIP file listing to skip packages without the desired files |
| Temporary storage config           | none                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| Persistent storage config          | Table Storage:<br />`PackageArchiveTableName`: ZIP directory and signature bytes are stored using MessagePack and [`WideEntityStorageService`](../../src/Logic/WideEntities/WideEntityService.cs)                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| Output CSV tables                  | none                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |

## Algorithm

A batch of catalog leaf items are passed to the driver. For each catalog leaf, MiniZip is used to fetch just the NuGet package (.nupkg) ZIP directory data and the (uncompressed) NuGet package signature. This is done via minimal HTTP HEAD and GET `Range` requests so that the whole package is not downloaded.

These two pieces of information (ZIP directory and signature) are serialized and compressed using MessagePack and then written into Azure Table Storage as "wide entities". A wide entity is a concept invented for this project so that small blobs can be segmented into Azure Table Storage. This is an alternative to writing blobs to Azure Blob Storage. Wide entities can be read and written in batches whereas blobs cannot. This improves performance and cost but has the drawback of being bounded in size.

The ZIP directory is represented using an [`MZip`](https://github.com/joelverhagen/MiniZip/blob/main/MiniZip/MZip/MZipFormat.cs) format, which is invented by the MiniZip library. It's essentially all bytes of the ZIP directory as well as a file offset in the original ZIP file. This allows a virtual `Stream` to be created of the ZIP file which allows ZIP reader APIs to fully explore the ZIP directory, but not read any ZIP entry content. The purpose of the format is to minimize the amount of data stored for each ZIP file but allow full inspection of some of the most important data in the ZIP, i.e. the ZIP central directory.