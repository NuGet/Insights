# PackageCompatibilities

This table contains the framework compatibility information for packages, using several different algorithms.
Package compatibility is a relatively deep subject with a lot of different approaches, caveats, and exceptions. This table
can help illustrate the differences between several approaches.

|                              |                                                                                                                                              |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.                                                                                                            |
| Child tables                 |                                                                                                                                              |
| Parent tables                |                                                                                                                                              |
| Column used for partitioning | Identity                                                                                                                                     |
| Data file container name     | packagecompatibilities                                                                                                                       |
| Driver implementation        | [`PackageCompatibilityToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageCompatibilityToCsv/PackageCompatibilityToCsvDriver.cs) |
| Record type                  | [`PackageCompatibility`](../../src/Worker.Logic/CatalogScan/Drivers/PackageCompatibilityToCsv/PackageCompatibility.cs)                       |

## Table schema

| Column name            | Data type        | Required                           | Description                                                            |
| ---------------------- | ---------------- | ---------------------------------- | ---------------------------------------------------------------------- |
| ScanId                 | string           | No                                 | Unused, always empty                                                   |
| ScanTimestamp          | timestamp        | No                                 | Unused, always empty                                                   |
| LowerId                | string           | Yes                                | Lowercase package ID. Good for joins                                   |
| Identity               | string           | Yes                                | Lowercase package ID and lowercase, normalized version. Good for joins |
| Id                     | string           | Yes                                | Original case package ID                                               |
| Version                | string           | Yes                                | Original case, normalized package version                              |
| CatalogCommitTimestamp | timestamp        | Yes                                | Latest catalog commit timestamp for the package                        |
| Created                | timestamp        | Yes, for non-Deleted               | When the package version was created                                   |
| ResultType             | enum             | Yes                                | Type of record (e.g. Available, Deleted)                               |
| HasError               | bool             | Yes                                | Whether or not there was an error determining compatibility            |
| NuspecReader           | array of strings | Yes, for Available unless HasError | Frameworks via `NuspecReader.GetSupportedFrameworks()`                 |
| NuGetGallery           | array of strings | Yes, for Available unless HasError | Frameworks via same logic a NuGetGallery                               |

If HasError is true, one or more of the framework fields may be empty. This may be for many reasons but the most common is that the package was packed in a non-standard way and has an invalid framework in one of the file paths.

Each target framework mentioned in this table is in the "short folder name" format, e.g. `netstandard2.0`. This format is succinct and well recognized due to it's usage in the actual package asset file paths.

## ResultType schema

| Enum value | Description                                                       |
| ---------- | ----------------------------------------------------------------- |
| Available  | The package is available and compatibility analysis was attempted |
| Deleted    | The package is deleted and no metadata is available               |

## NuspecReader schema

The NuspecReader column is an array of strings where each string is a NuGet target framework that the package is compatible with, as determined by NuGet/NuGet.Client [`NuspecReader.GetSupportedFrameworks()`](https://github.com/NuGet/NuGet.Client/blob/5353034fed272cb81fbe60326f334b99871d8b74/src/NuGet.Core/NuGet.Packaging/PackageReaderBase.cs#L367).

This method has known problems such as not considering newer top level folders like `buildTransitive`.

## NuGetGallery schema

The NuspecReader column is an array of strings where each string is a NuGet target framework that the package is compatible with, as determined by NuGet/NuGetGallery [`PackageService.GetSupportedFrameworks()`](https://github.com/NuGet/NuGetGallery/blob/7557469186f07c1a15fff57e5efd3816e622a776/src/NuGetGallery.Services/PackageManagement/PackageService.cs#L720-L818).

This solution is meant to provide a view of framework compatibility that considers several popular package types but does not expand all compatible frameworks. For example, a package only containing a `netstandard1.0` DLL will show only `netstandard1.0`, not all of the frameworks that support `netstandard1.0` implicitly.
