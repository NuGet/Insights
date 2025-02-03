# GitHubUsages

This table contains information about GitHub repositories that use packages on NuGet.org. This data comes from the same
source as the "Used by" tab on NuGet.org package details page. The backend job that NuGet.org uses for the scanning is
called [NuGet.Jobs.GitHubIndexer](https://github.com/NuGet/NuGetGallery/tree/main/src/NuGet.Jobs.GitHubIndexer).

There are several caveats with this data:
- Version information is not available. Matches are only on package ID.
- The match on GitHub is only based on the package ID, and not also package source. For generic package names, it is
  possible a match is found for a package from a non-NuGet.org package source. Package source information (e.g.
  NuGet.config `<packageSources>`) is not checked.
- GitHub repositories below a certain "star" threshold are not scanned and therefore won't appear in this data. The star
  threshold is currently 100 stars.


|                                    |                                                                                                                   |
| ---------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| Cardinality                        | At least one row per unique package ID on NuGet.                                                                  |
| Child tables                       |                                                                                                                   |
| Parent tables                      |                                                                                                                   |
| Column used for CSV partitioning   | LowerId                                                                                                           |
| Column used for Kusto partitioning | LowerId                                                                                                           |
| Key fields                         | LowerId, Repository                                                                                               |
| Data file container name           | githubusage                                                                                                       |
| Driver                             | [`GitHubUsageToCsvUpdater`](../../src/Worker.Logic/MessageProcessors/GitHubUsageToCsv/GitHubUsageToCsvUpdater.cs) |
| Record type                        | [`GitHubUsageRecord`](../../src/Worker.Logic/MessageProcessors/GitHubUsageToCsv/GitHubUsageRecord.cs)             |

## Table schema

| Column name   | Data type | Required                 | Description                                                                              |
| ------------- | --------- | ------------------------ | ---------------------------------------------------------------------------------------- |
| AsOfTimestamp | timestamp | No                       | Unused, always empty                                                                     |
| LowerId       | string    | Yes                      | Lowercase package ID. Good for joins                                                     |
| Id            | string    | Yes                      | Original case package ID for an arbitrary package version                                |
| ResultType    | enum      | Yes                      | Type of record (e.g. GitHubDependent, NoGitHubDependent)                                 |
| Repository    | string    | Yes, for GitHubDependent | GitHub repository depending on the package, in the form of "owner/repo", original casing |
| Stars         | int       | Yes, for GitHubDependent | Number of GitHub stars the repository has, last time it was scanned                      |

## ResultType schema

| Enum value        | Description                                                                |
| ----------------- | -------------------------------------------------------------------------- |
| GitHubDependent   | The package ID has at least one popular GitHub repository depending on it. |
| NoGitHubDependent | The package ID has no popular GitHub repositories depending on it.         |
