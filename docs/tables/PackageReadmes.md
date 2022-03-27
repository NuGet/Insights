# PackageReadmes

This table contains metadata and full content of NuGet package README files. These files can come from one of two sources:

1. Embedded README files, which are packed into the NuGet package and are immutable per version
1. Legacy README files, which are mutable per package version and are uploaded to NuGet.org separately from the package

|                              |                                                                                                                         |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.org                                                                                    |
| Child tables                 |                                                                                                                         |
| Parent tables                |                                                                                                                         |
| Column used for partitioning | Identity                                                                                                                |
| Data file container name     | packagereadmes                                                                                                          |
| Driver implementation        | [`PackageReadmeToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageReadmeToCsv/PackageReadmeToCsvDriver.cs) |
| Record type                  | [`PackageReadme`](../../src/Worker.Logic/CatalogScan/Drivers/PackageReadmeToCsv/PackageReadme.cs)                       |

## Table schema

| Column name            | Data type | Required                    | Description                                                            |
| ---------------------- | --------- | --------------------------- | ---------------------------------------------------------------------- |
| ScanId                 | string    | No                          | Unused, always empty                                                   |
| ScanTimestamp          | timestamp | No                          | Unused, always empty                                                   |
| LowerId                | string    | Yes                         | Lowercase package ID. Good for joins                                   |
| Identity               | string    | Yes                         | Lowercase package ID and lowercase, normalized version. Good for joins |
| Id                     | string    | Yes                         | Original case package ID                                               |
| Version                | string    | Yes                         | Original case, normalized package version                              |
| CatalogCommitTimestamp | timestamp | Yes                         | Latest catalog commit timestamp for the package                        |
| Created                | timestamp | Yes, for non-Deleted        | When the package version was created                                   |
| ResultType             | enum      | Yes                         | Type of record (e.g. Deleted, None, Legacy, Embedded)                  |
| Size                   | int       | Yes, for Legacy or Embedded | Size in bytes of the README file                                       |
| LastModified           | timestamp | Yes, for Legacy or Embedded | Last modified header found on the README file                          |
| SHA256                 | string    | Yes, for Legacy or Embedded | SHA-256 of the README bytes                                            |
| Content                | string    | Yes, for Legacy or Embedded | Full string content of the README                                      |

## ResultType schema

| Enum value | Description                                         |
| ---------- | --------------------------------------------------- |
| Deleted    | The package is deleted and no metadata is available |
| Embedded   | The package has an embedded README                  |
| Legacy     | The package has a legacy (non-embedded) README      |
| None       | The package has no README                           |
