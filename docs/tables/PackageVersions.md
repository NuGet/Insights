# PackageVersions

This table has parsed information about each package version, as well as it's "latest" status within each respective package ID. This table allows you to reason about SemVer order of package versions without having native SemVer ordering or parsing support in your data or query system.

|                              |                                                                                                                            |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.org                                                                                       |
| Child tables                 |                                                                                                                            |
| Parent tables                |                                                                                                                            |
| Column used for partitioning | LowerId                                                                                                                    |
| Data file container name     | packageversions                                                                                                            |
| Driver implementation        | [`PackageVersionToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageVersionToCsv/PackageVersionToCsvDriver.cs) |
| Record type                  | [`PackageVersionRecord`](../../src/Worker.Logic/CatalogScan/Drivers/PackageVersionToCsv/PackageVersionRecord.cs)           |

## Table schema

| Column name            | Data type        | Required              | Description                                                                                                                                                        |
| ---------------------- | ---------------- | --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| ScanId                 | string           | No                    | Unused, always empty                                                                                                                                               |
| ScanTimestamp          | timestamp        | No                    | Unused, always empty                                                                                                                                               |
| LowerId                | string           | Yes                   | Lowercase package ID. Good for joins                                                                                                                               |
| Identity               | string           | Yes                   | Lowercase package ID and lowercase, normalized version. Good for joins                                                                                             |
| Id                     | string           | Yes                   | Original case package ID                                                                                                                                           |
| Version                | string           | Yes                   | Original case, normalized package version                                                                                                                          |
| CatalogCommitTimestamp | timestamp        | Yes                   | Latest catalog commit timestamp for the package                                                                                                                    |
| Created                | timestamp        | Yes, for Available    | When the package version was created                                                                                                                               |
| ResultType             | enum             | Yes                   | Type of record (e.g. Available, Deleted)                                                                                                                           |
| OriginalVersion        | string           | Yes, for Available    | Original package version, non-normalized                                                                                                                           |
| FullVersion            | string           | Yes, for Available    | Full version string, normalized but can include SemVer 2.0.0 build metadata                                                                                        |
| Major                  | int              | Yes                   | Major version number, this is the first digit                                                                                                                      |
| Minor                  | int              | Yes                   | Minor version number, this is the second digit                                                                                                                     |
| Patch                  | int              | Yes                   | Patch version number, this is the third digit                                                                                                                      |
| Revision               | int              | Yes                   | Revision version number, this is the fourth digit, defaults to 0                                                                                                   |
| Release                | string           | Yes, for IsPrerelease | Prerelease label of the version string, this is after the hyphen and before any plus                                                                               |
| ReleaseLabels          | array of strings | Yes, for IsPrerelease | The components of the Release columns, split by dots                                                                                                               |
| Metadata               | string           | No                    | The build metadata component, this is after any plus, it is not used for comparison                                                                                |
| IsPrerelease           | bool             | Yes                   | Whether or not the version is prerelease, implied by non-empty Release column                                                                                      |
| IsListed               | bool             | Yes, for Available    | Whether or not this version is listed, impacts "latest" status                                                                                                     |
| IsSemVer2              | bool             | Yes, for Available    | Whether or not this package is [SemVer 2.0.0](https://docs.microsoft.com/en-us/nuget/concepts/package-versioning#semantic-versioning-200), impacts "latest" status |
| SemVerType             | flags enum       | Yes, for Available    | What part of the package metadata made it SemVer 2.0.0, if at all                                                                                                  |
| SemVerOrder            | int              | Yes                   | Within this package ID, what is the position (0-based index) of the versions sorted in SemVer order                                                                |
| IsLatest               | bool             | Yes                   | Whether this package is the latest listed, stable or prerelease, SemVer 1.0.0 package                                                                              |
| IsLatestStable         | bool             | Yes                   | Whether this package is the latest listed, stable, SemVer 1.0.0 package                                                                                            |
| IsLatestSemVer2        | bool             | Yes                   | Whether this package is the latest listed, stable or prerelease, SemVer 1.0.0 or SemVer 2.0.0 package                                                              |
| IsLatestStableSemVer2  | bool             | Yes                   | Whether this package is the latest listed, stable, SemVer 1.0.0 or SemVer 2.0.0 package                                                                            |

## ResultType schema

The ResultType enum indicates the possible variants of records.

| Enum value | Description                                         |
| ---------- | --------------------------------------------------- |
| Available  | The package is available and processed successfully |
| Deleted    | The package is deleted and no metadata is available |

## SemVerType schema

The SemVerType enum has one or more of the the following values, separated by a comma and a space.

| Enum value                     | Description                                                                                             |
| ------------------------------ | ------------------------------------------------------------------------------------------------------- |
| DependencyMaxHasBuildMetadata  | The package is SemVer 2.0.0 because the dependency version range maximum has build metadata             |
| DependencyMaxHasPrereleaseDots | The package is SemVer 2.0.0 because the prerelease label of a dependency version range maximum has dots |
| DependencyMinHasBuildMetadata  | The package is SemVer 2.0.0 because the dependency version range minimum has build metadata             |
| DependencyMinHasPrereleaseDots | The package is SemVer 2.0.0 because the prerelease label of a dependency version range minimum has dots |
| SemVer1                        | The package is SemVer 1.0.0                                                                             |
| VersionHasBuildMetadata        | The package is SemVer 2.0.0 because there is build metadata                                             |
| VersionHasPrereleaseDots       | The package is SemVer 2.0.0 because the prerelease label has dots                                       |
