# SymbolPackageFiles

This table contains hashes for every ZIP archive entries in the .snupkg (symbol package). Note that not all packages
have symbol packages. This is a sibling table to [SymbolPackageArchiveEntries](SymbolPackageArchiveEntries.md),
containing details on the file contents instead of just ZIP entry metadata.

|                                    |                                                                                                               |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| Cardinality                        | One or more rows per package, more than one if the symbol package has multiple files in the ZIP (most do)     |
| Child tables                       |                                                                                                               |
| Parent tables                      |                                                                                                               |
| Column used for CSV partitioning   | Identity                                                                                                      |
| Column used for Kusto partitioning | Identity                                                                                                      |
| Key fields                         | Identity, SequenceNumber                                                                                      |
| Data file container name           | symbolpackagefiles                                                                                            |
| Driver                             | [`SymbolPackageFileToCsv`](../drivers/SymbolPackageFileToCsv.md)                                              |
| Record type                        | [`SymbolPackageFileRecord`](../../src/Worker.Logic/Drivers/SymbolPackageFileToCsv/SymbolPackageFileRecord.cs) |

## Table schema

| Column name              | Data type | Required             | Description                                                               |
| ------------------------ | --------- | -------------------- | ------------------------------------------------------------------------- |
| ScanId                   | string    | No                   | Unused, always empty                                                      |
| ScanTimestamp            | timestamp | No                   | Unused, always empty                                                      |
| LowerId                  | string    | Yes                  | Lowercase package ID. Good for joins                                      |
| Identity                 | string    | Yes                  | Lowercase package ID and lowercase, normalized version. Good for joins    |
| Id                       | string    | Yes                  | Original case package ID                                                  |
| Version                  | string    | Yes                  | Original case, normalized package version                                 |
| CatalogCommitTimestamp   | timestamp | Yes                  | Latest catalog commit timestamp for the package                           |
| Created                  | timestamp | Yes, for Available   | When the package version was created                                      |
| ResultType               | enum      | Yes                  | Type of record (e.g. Available, Deleted, DoesNotExist, InvalidZipEntry)   |
| SequenceNumber           | int       | Yes, for ZIP entries | The index of this entry within the whole ZIP file                         |
| Path                     | string    | Yes, for ZIP entries | The relative file path within the ZIP file                                |
| FileName                 | string    | Yes, for ZIP entries | The file name from the Path                                               |
| FileExtension            | string    | Yes, for ZIP entries | The file extension from the Path                                          |
| TopLevelFolder           | string    | Yes, for ZIP entries | The first folder (i.e. directory) name from the Path                      |
| CompressedLength         | long      | Yes, for ZIP entries | The compressed size of the file                                           |
| EntryUncompressedLength  | long      | Yes, for ZIP entries | The uncompressed size of the file                                         |
| ActualUncompressedLength | long      | Yes, for Available   | The uncompressed size of the file                                         |
| SHA256                   | string    | Yes, for Available   | Base64 encoded SHA256 hash of the file bytes                              |
| First16Bytes             | string    | Yes, for Available   | Base64 encoded first 16 bytes of the file, useful for file type detection |

Records are referred to as "ZIP entries" in the table above if it does not have ResultType `Deleted` or `DoesNotExist`.

## ResultType schema

| Enum value      | Description                                                     |
| --------------- | --------------------------------------------------------------- |
| Available       | The package is available and processed successfully             |
| Deleted         | The package is deleted and no metadata is available             |
| DoesNotExist    | The package does not have a symbol package                      |
| InvalidZipEntry | The file could not be analyzed due to an error in the ZIP entry |
