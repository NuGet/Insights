# PackageDeprecations

This table has the latest deprecation status for every package. Package deprecation is a way of marking a package as not longer intended for use but is short of deleting or (necessarily) unlisting the package. Packages that are currently deprecated will have a `ResultType` of `Deprecated`.

|                              |                                                                                                                       |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.                                                                                     |
| Child tables                 |                                                                                                                       |
| Parent tables                |                                                                                                                       |
| Column used for partitioning | Identity                                                                                                              |
| Data file container name     | packagedeprecations                                                                                                   |
| Driver implementation        | [`CatalogDataToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/CatalogDataToCsv/CatalogDataToCsvDriver.cs)     |
| Record type                  | [`PackageDeprecationRecord`](../../src/Worker.Logic/CatalogScan/Drivers/CatalogDataToCsv/PackageDeprecationRecord.cs) |

## Table schema

| Column name            | Data type        | Required             | Description                                                            |
| ---------------------- | ---------------- | -------------------- | ---------------------------------------------------------------------- |
| ScanId                 | string           | No                   | Unused, always empty                                                   |
| ScanTimestamp          | timestamp        | No                   | Unused, always empty                                                   |
| LowerId                | string           | Yes                  | Lowercase package ID. Good for joins                                   |
| Identity               | string           | Yes                  | Lowercase package ID and lowercase, normalized version. Good for joins |
| Id                     | string           | Yes                  | Original case package ID                                               |
| Version                | string           | Yes                  | Original case, normalized package version                              |
| CatalogCommitTimestamp | timestamp        | Yes                  | Latest catalog commit timestamp for the package                        |
| Created                | timestamp        | Yes, for non-Deleted | When the package version was created                                   |
| ResultType             | enum             | Yes                  | Type of record (e.g. NotDeprecated, Deprecated, Deleted)               |
| Message                | string           | Yes, for Deprecated  | The deprecation message provided by the package owner                  |
| Reasons                | array of strings | Yes, for Deprecated  | The reasons for deprecation.                                           |
| AlternatePackageId     | string           | No                   | The package ID of the alternate package that should be used instead    |
| AlternateVersionRange  | string           | No                   | The version range of the package that is an applicable alternative     |

## ResultType schema

| Enum value    | Description                                         |
| ------------- | --------------------------------------------------- |
| Deleted       | The package is deleted and no metadata is available |
| Deprecated    | The package is available but is deprecated.         |
| NotDeprecated | The package is available and not deprecated.        |

## Reasons schema

The Reasons column is an array of strings where each string is a reason for why the package is deprecated. Deprecated packages have at least one reason but can have more than one.

## Reason schema

| Enum value   | Description                                                              |
| ------------ | ------------------------------------------------------------------------ |
| Other        | The package is deprecated because of some other undefined reason.        |
| Legacy       | The package is deprecated because it is legacy and no longer maintained. |
| CriticalBugs | The package is deprecated because it has critical bugs.                  |
