# SymbolPackageArchiveEntries

This table contains metadata about the ZIP archive entries in the .snupkg (symbol package). This is low level ZIP
metadata minimal special knowledge of NuGet-specific interpretation of the ZIP file entries. Note that not all
packages have symbol packages.

|                                    |                                                                                                                      |
| ---------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Cardinality                        | One or more rows per package, more than one if the symbol package has multiple files in the ZIP (most do)            |
| Child tables                       |                                                                                                                      |
| Parent tables                      | [SymbolPackageArchives](SymbolPackageArchives.md) joined on Identity                                                 |
| Column used for CSV partitioning   | Identity                                                                                                             |
| Column used for Kusto partitioning | Identity                                                                                                             |
| Key fields                         | Identity, SequenceNumber                                                                                             |
| Data file container name           | symbolpackagearchiveentries                                                                                          |
| Driver                             | [`SymbolPackageArchiveToCsv`](../drivers/SymbolPackageArchiveToCsv.md)                                               |
| Record type                        | [`SymbolPackageArchiveEntry`](../../src/Worker.Logic/Drivers/SymbolPackageArchiveToCsv/SymbolPackageArchiveEntry.cs) |

## Table schema

| Column name            | Data type | Required           | Description                                                                                                          |
| ---------------------- | --------- | ------------------ | -------------------------------------------------------------------------------------------------------------------- |
| ScanId                 | string    | No                 | Unused, always empty                                                                                                 |
| ScanTimestamp          | timestamp | No                 | Unused, always empty                                                                                                 |
| LowerId                | string    | Yes                | Lowercase package ID. Good for joins                                                                                 |
| Identity               | string    | Yes                | Lowercase package ID and lowercase, normalized version. Good for joins                                               |
| Id                     | string    | Yes                | Original case package ID                                                                                             |
| Version                | string    | Yes                | Original case, normalized package version                                                                            |
| CatalogCommitTimestamp | timestamp | Yes                | Latest catalog commit timestamp for the package                                                                      |
| Created                | timestamp | Yes, for Available | When the package version was created                                                                                 |
| ResultType             | enum      | Yes                | Type of record (e.g. Available, Deleted, DoesNotExist)                                                               |
| SequenceNumber         | int       | Yes, for Available | The index of this entry within the whole ZIP file                                                                    |
| Path                   | string    | Yes, for Available | The relative file path within the ZIP file                                                                           |
| FileName               | string    | Yes, for Available | The file name from the Path                                                                                          |
| FileExtension          | string    | Yes, for Available | The file extension from the Path                                                                                     |
| TopLevelFolder         | string    | Yes, for Available | The first folder (i.e. directory) name from the Path                                                                 |
| Flags                  | ushort    | Yes, for Available | The flags, per [APPNOTE.TXT](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT) section 4.4.4              |
| CompressionMethod      | ushort    | Yes, for Available | The compression method, per [APPNOTE.TXT](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT) section 4.4.5 |
| LastModified           | timestamp | Yes, for Available | The last modified timestamp, defaults to `1980-01-01T00:00:00`                                                       |
| Crc32                  | uint      | Yes, for Available | CRC-32 checksum of the uncompressed file entry                                                                       |
| CompressedSize         | uint      | Yes, for Available | Size of the compressed file entry in bytes                                                                           |
| UncompressedSize       | uint      | Yes, for Available | Size of the uncompressed file entry in bytes                                                                         |
| LocalHeaderOffset      | uint      | Yes, for Available | The byte offset to the local file header                                                                             |
| Comment                | string    | No                 | The comment on the file entry                                                                                        |

## ResultType schema

| Enum value   | Description                                         |
| ------------ | --------------------------------------------------- |
| Available    | The package is available and processed successfully |
| Deleted      | The package is deleted and no metadata is available |
| DoesNotExist | The package does not have a symbol package          |
