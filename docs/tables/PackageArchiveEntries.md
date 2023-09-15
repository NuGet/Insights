# PackageArchiveEntries

This table contains metadata about the ZIP archive entries in the .nupkg. This is low level ZIP metadata minimal special
knowledge of NuGet-specific interpretation of the ZIP file entries.

|                              |                                                                                                                |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | One or more rows per package, more than one if the package has multiple files in the ZIP (most do)             |
| Child tables                 |                                                                                                                |
| Parent tables                | [PackageArchives](PackageArchives.md) joined on Identity                                                       |
| Column used for partitioning | Identity                                                                                                       |
| Data file container name     | packagearchiveentries                                                                                          |
| Driver                       | [`PackageArchiveToCsv`](../drivers/PackageArchiveToCsv.md)                                                     |
| Record type                  | [`PackageArchiveEntry`](../../src/Worker.Logic/CatalogScan/Drivers/PackageArchiveToCsv/PackageArchiveEntry.cs) |

## Table schema

| Column name            | Data type | Required       | Description                                                                                                          |
| ---------------------- | --------- | -------------- | -------------------------------------------------------------------------------------------------------------------- |
| ScanId                 | string    | No             | Unused, always empty                                                                                                 |
| ScanTimestamp          | timestamp | No             | Unused, always empty                                                                                                 |
| LowerId                | string    | Yes            | Lowercase package ID. Good for joins                                                                                 |
| Identity               | string    | Yes            | Lowercase package ID and lowercase, normalized version. Good for joins                                               |
| Id                     | string    | Yes            | Original case package ID                                                                                             |
| Version                | string    | Yes            | Original case, normalized package version                                                                            |
| CatalogCommitTimestamp | timestamp | Yes            | Latest catalog commit timestamp for the package                                                                      |
| Created                | timestamp | Yes, Available | When the package version was created                                                                                 |
| ResultType             | enum      | Yes            | Type of record (e.g. Available, Deleted)                                                                             |
| SequenceNumber         | int       | Yes, Available | The index of this entry within the whole ZIP file                                                                    |
| Path                   | string    | Yes, Available | The relative file path within the ZIP file                                                                           |
| FileName               | string    | Yes, Available | The file name from the Path                                                                                          |
| FileExtension          | string    | Yes, Available | The file extension from the Path                                                                                     |
| TopLevelFolder         | string    | Yes, Available | The first folder (i.e. directory) name from the Path                                                                 |
| Flags                  | ushort    | Yes, Available | The flags, per [APPNOTE.TXT](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT) section 4.4.4              |
| CompressionMethod      | ushort    | Yes, Available | The compression method, per [APPNOTE.TXT](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT) section 4.4.5 |
| LastModified           | timestamp | Yes, Available | The last modified timestamp, defaults to `1980-01-01T00:00:00`                                                       |
| Crc32                  | uint      | Yes, Available | CRC-32 checksum of the uncompressed file entry                                                                       |
| CompressedSize         | uint      | Yes, Available | Size of the compressed file entry in bytes                                                                           |
| UncompressedSize       | uint      | Yes, Available | Size of the uncompressed file entry in bytes                                                                         |
| LocalHeaderOffset      | uint      | Yes, Available | The byte offset to the local file header                                                                             |
| Comment                | string    | No             | The comment on the file entry                                                                                        |

## ResultType schema

The ResultType enum indicates the possible variants of records.

| Enum value   | Description                                         |
| ------------ | --------------------------------------------------- |
| Available    | The package is available and processed successfully |
| Deleted      | The package is deleted and no metadata is available |
| DoesNotExist | Unused                                              |
