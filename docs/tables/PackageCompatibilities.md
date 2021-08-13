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

| Column name            | Data type        | Required                           | Description                                                                                                                                                                                                                      |
| ---------------------- | ---------------- | ---------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ScanId                 | string           | No                                 | Unused, always empty                                                                                                                                                                                                             |
| ScanTimestamp          | timestamp        | No                                 | Unused, always empty                                                                                                                                                                                                             |
| LowerId                | string           | Yes                                | Lowercase package ID. Good for joins                                                                                                                                                                                             |
| Identity               | string           | Yes                                | Lowercase package ID and lowercase, normalized version. Good for joins                                                                                                                                                           |
| Id                     | string           | Yes                                | Original case package ID                                                                                                                                                                                                         |
| Version                | string           | Yes                                | Original case, normalized package version                                                                                                                                                                                        |
| CatalogCommitTimestamp | timestamp        | Yes                                | Latest catalog commit timestamp for the package                                                                                                                                                                                  |
| Created                | timestamp        | Yes, for non-Deleted               | When the package version was created                                                                                                                                                                                             |
| ResultType             | enum             | Yes                                | Type of record (e.g. Available, Deleted)                                                                                                                                                                                         |
| HasError               | bool             | Yes                                | Whether or not there was an error determining compatibility                                                                                                                                                                      |
| DoesNotRoundTrip       | bool             | Yes                                | Whether or not the frameworks found fail round trip when parsed                                                                                                                                                                  |
| HasAny                 | bool             | Yes                                | Whether or not any of the compatibility lists have an [`any` framework](https://github.com/NuGet/NuGet.Client/blob/cb290a427545a9214f22d9d6196c9e3c81380611/src/NuGet.Core/NuGet.Frameworks/NuGetFramework.cs#L427-L433)         |
| HasUnsupported         | bool             | Yes                                | Whether or not any of the compatibility lists have an [`unsupported` framework](https://github.com/NuGet/NuGet.Client/blob/cb290a427545a9214f22d9d6196c9e3c81380611/src/NuGet.Core/NuGet.Frameworks/NuGetFramework.cs#L411-L417) |
| HasAgnostic            | bool             | Yes                                | Whether or not the of the compatibility lists have an [`agnostic` framework](https://github.com/NuGet/NuGet.Client/blob/cb290a427545a9214f22d9d6196c9e3c81380611/src/NuGet.Core/NuGet.Frameworks/NuGetFramework.cs#L419-L425)    |
| BrokenFrameworks       | array of strings | Yes, for Available unless HasError | Framework strings found that don't roundtrip properly                                                                                                                                                                            |
| NuspecReader           | array of strings | Yes, for Available unless HasError | Frameworks via `NuspecReader.GetSupportedFrameworks`                                                                                                                                                                             |
| NU1202                 | array of strings | Yes, for Available unless HasError | Frameworks mentioned in the NU1202 error                                                                                                                                                                                         |
| NuGetGallery           | array of strings | Yes, for Available unless HasError | Frameworks via same logic a NuGetGallery **except `any` is included**, with URL-encoded paths decoded                                                                                                                                                         |
| NuGetGalleryEscaped    | array of strings | Yes, for Available unless HasError | Frameworks via same logic a NuGetGallery **except `any` is included**, with URL-encoded left encoded                                                                                                                                                          |

If HasError is true, one or more of the framework columns may be empty. This may be for many reasons but the most common is that the package was packed in a non-standard way and has an invalid framework in one of the file paths.

Any of the arrays of strings may be empty. A null value indicates an error when generating the array or that the package is deleted.

Each target framework mentioned in this table is in the "short folder name" format, e.g. `netstandard2.0`. This format is succinct and well recognized due to it's usage in the actual package asset file paths.

## ResultType schema

| Enum value | Description                                                       |
| ---------- | ----------------------------------------------------------------- |
| Available  | The package is available and compatibility analysis was attempted |
| Deleted    | The package is deleted and no metadata is available               |

## BrokenFrameworks schema

The BrokenFrameworks column contains frameworks that fail to round trip properly through `NuGetFrameworks.Parse(...)`. This can lead to strange behavior in NuGet.

The BrokenFrameworks column may contain frameworks in the full framework format (e.g. `{name},Version=v{version}`) to fully illustrate the broken values or in the short folder name format.

## NuspecReader schema

The NuspecReader column is an array of strings where each string is a NuGet target framework that the package is compatible with, as determined by NuGet/NuGet.Client [`NuspecReader.GetSupportedFrameworks`](https://github.com/NuGet/NuGet.Client/blob/5353034fed272cb81fbe60326f334b99871d8b74/src/NuGet.Core/NuGet.Packaging/PackageReaderBase.cs#L367).

This method has known problems such as not considering newer top level folders like `buildTransitive`.

## NU1202 schema

The NU1202 column is an array of strings where each string is a NuGet target framework that the package is compatible with, as determined by NuGet/NuGet.Client [`CompatibilityChecker.GetPackageFrameworks`](https://github.com/NuGet/NuGet.Client/blob/1e359e8d48e4f7d13e106b1a9178ed3622a3a0b8/src/NuGet.Core/NuGet.Commands/RestoreCommand/CompatibilityChecker.cs#L252-L294). This method is used to generate the [NU1202](https://docs.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu1202) message emitted when restoring an incompatible package.

## NuGetGallery schema

The NuspecReader column is an array of strings where each string is a NuGet target framework that the package is compatible with, as determined by NuGet/NuGetGallery [`PackageService.GetSupportedFrameworks()`](https://github.com/NuGet/NuGetGallery/blob/7557469186f07c1a15fff57e5efd3816e622a776/src/NuGetGallery.Services/PackageManagement/PackageService.cs#L720-L818).

Note that the data stored in the NuGetGallery (nuget.org) database excludes `any` frameworks as well as retains the original framework texts for `unsupported` cases. This column includes the `any` frameworks and shows unsupported frameworks simply as `unsupported` instead of the weird, unsupported framework text. To see the original, unsupported framework text, look at the BrokenFrameworks column.

This solution is meant to provide a view of framework compatibility that considers several popular package types but does not expand all compatible frameworks. For example, a package only containing a `netstandard1.0` DLL will show only `netstandard1.0`, not all of the frameworks that support `netstandard1.0` implicitly.

## NuGetGalleryEscaped schema

Same as the NuGetGallerySchema column except the logic was executed on file paths that were not URL decoded. The reason this column exists is because naive analysis of NuGet package ZIP paths may result in leaving URL-encoded paths as-is. This can lead to differences in how frameworks in file paths are parsed. NuGet/NuGet.Client `PackageArchiveReader` decodes file paths containing the `%` sign ([source](https://github.com/NuGet/NuGet.Client/blob/1e359e8d48e4f7d13e106b1a9178ed3622a3a0b8/src/NuGet.Core/NuGet.Packaging/PackageExtraction/ZipArchiveExtensions.cs#L30-L48)) so this column illustrates what analysis would look like if this decoding do NOT happen.