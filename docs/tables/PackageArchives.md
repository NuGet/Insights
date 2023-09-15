# PackageArchives

This table contains metadata about the ZIP archive that is the package .nupkg. This is low level ZIP metadata without
any special knowledge of NuGet-specific interpretation of the ZIP file.

|                              |                                                                                                                  |
| ---------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.org                                                                             |
| Child tables                 | [PackageArchiveEntries](PackageArchiveEntries.md) joined on Identity                                             |
| Parent tables                |                                                                                                                  |
| Column used for partitioning | Identity                                                                                                         |
| Data file container name     | packagearchives                                                                                                  |
| Driver                       | [`PackageArchiveToCsv`](../drivers/PackageArchiveToCsv.md)                                                       |
| Record type                  | [`PackageArchiveRecord`](../../src/Worker.Logic/CatalogScan/Drivers/PackageArchiveToCsv/PackageArchiveRecord.cs) |

## Table schema

| Column name                      | Data type | Required           | Description                                                                               |
| -------------------------------- | --------- | ------------------ | ----------------------------------------------------------------------------------------- |
| ScanId                           | string    | No                 | Unused, always empty                                                                      |
| ScanTimestamp                    | timestamp | No                 | Unused, always empty                                                                      |
| LowerId                          | string    | Yes                | Lowercase package ID. Good for joins                                                      |
| Identity                         | string    | Yes                | Lowercase package ID and lowercase, normalized version. Good for joins                    |
| Id                               | string    | Yes                | Original case package ID                                                                  |
| Version                          | string    | Yes                | Original case, normalized package version                                                 |
| CatalogCommitTimestamp           | timestamp | Yes                | Latest catalog commit timestamp for the package                                           |
| Created                          | timestamp | Yes, for Available | When the package version was created                                                      |
| ResultType                       | enum      | Yes                | Type of record (e.g. Available, Deleted)                                                  |
| Size                             | long      | Yes, for Available | The size of the .nupkg in bytes                                                           |
| OffsetAfterEndOfCentralDirectory | long      | Yes, for Available | Byte offset after the end of central directory signature, typically 18 less than the Size |
| CentralDirectorySize             | uint      | Yes, for Available | Size of central directory in bytes                                                        |
| OffsetOfCentralDirectory         | uint      | Yes, for Available | Byte offset of the central directory                                                      |
| EntryCount                       | int       | Yes, for Available | The number of entries in the ZIP                                                          |
| Comment                          | string    | No                 | The comment at the end of the ZIP archive                                                 |
| HeaderMD5                        | string    | No                 | The Base64 encoded MD5 hash of the .nupkg from the HTTP header                            |
| HeaderSHA512                     | string    | No                 | The Base64 encoded SHA512 hash of the .nupkg from the HTTP header                         |
| MD5                              | string    | Yes, for Available | The Base64 encoded MD5 hash of the .nupkg                                                 |
| SHA1                             | string    | Yes, for Available | The Base64 encoded SHA1 hash of the .nupkg                                                |
| SHA256                           | string    | Yes, for Available | The Base64 encoded SHA256 hash of the .nupkg                                              |
| SHA512                           | string    | Yes, for Available | The Base64 encoded SHA512 hash of the .nupkg                                              |

## ResultType schema

The ResultType enum indicates the possible variants of records.

| Enum value   | Description                                         |
| ------------ | --------------------------------------------------- |
| Available    | The package is available and processed successfully |
| Deleted      | The package is deleted and no metadata is available |
| DoesNotExist | Unused                                              |
