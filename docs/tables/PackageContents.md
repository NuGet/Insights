# PackageContents

This table contains a subset of package content within each .nupkg on NuGet.org. The content included in the table is
configurable by file extension and maximum cumulative file size per package.

|                              |                                                                                                                             |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | One or more rows per package, more than one if the package has multiple recognized assets                                   |
| Child tables                 |                                                                                                                             |
| Parent tables                |                                                                                                                             |
| Column used for partitioning | Identity                                                                                                                    |
| Data file container name     | packagecontents                                                                                                              |
| Driver implementation        | [`PackageContentToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageContentToCsv/PackageContentToCsvDriver.cs ) |
| Record type                  | [`PackageContent`](../../src/Worker.Logic/CatalogScan/Drivers/PackageContentToCsv/PackageContent.cs)                        |

## Table schema

| Column name            | Data type | Required                                            | Description                                                                                                                  |
| ---------------------- | --------- | --------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| ScanId                 | string    | No                                                  | Unused, always empty                                                                                                         |
| ScanTimestamp          | timestamp | No                                                  | Unused, always empty                                                                                                         |
| LowerId                | string    | Yes                                                 | Lowercase package ID. Good for joins                                                                                         |
| Identity               | string    | Yes                                                 | Lowercase package ID and lowercase, normalized version. Good for joins                                                       |
| Id                     | string    | Yes                                                 | Original case package ID                                                                                                     |
| Version                | string    | Yes                                                 | Original case, normalized package version                                                                                    |
| CatalogCommitTimestamp | timestamp | Yes                                                 | Latest catalog commit timestamp for the package                                                                              |
| Created                | timestamp | Yes, for non-Deleted                                | When the package version was created                                                                                         |
| ResultType             | enum      | Yes                                                 | Type of record (e.g. AllLoaded, Deleted)                                                                                     |
| Path                   | string    | Yes, for available                                  | The file path of the content                                                                                                 |
| FileExtension          | string    | Yes, for available                                  | The file extension of the file, may be an empty string, typically starts with a dot                                          |
| SequenceNumber         | int       | Yes, for available                                  | The sequence number of the entry in the .nupkg ZIP. There will be between values                                             |
| Size                   | int       | Yes, for available                                  | The uncompressed size of the file                                                                                            |
| Truncated              | bool      | Yes, for available                                  | Whether or not the file was partially read or not read at all due to the cumulative file size limit                          |
| TruncatedSize          | int       | Yes, when Truncated                                 | The number of bytes read before truncating the content                                                                       |
| SHA256                 | string    | Yes, for available                                  | The SHA256 hash of the whole file (correct even if Truncated is true)                                                        |
| Content                | string    | Yes, when TruncatedSize > 0 and no DupicatedContent | The content of the file as a string, truncated by character if Truncated is true                                             |
| DuplicateContent       | bool      | Yes, for available                                  | Whether or not this record SHA256 has already appeared in the package. If true, look for another record with matching SHA256 |

## ResultType schema

A result is considered available if it AllLoaded or PartiallyLoaded.

| Enum value      | Description                                                     |
| --------------- | --------------------------------------------------------------- |
| AllLoaded       | All matching content files for the package were loaded          |
| Deleted         | The package is deleted and therefore has no content files       |
| NoContent       | The package has no matching content files                       |
| PartiallyLoaded | At least one of the content files for the package was truncated |
