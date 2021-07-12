# PackageVulnerabilities

This table has the package vulnerability information associated with individual package versions.

Package vulnerability information on NuGet.org is sourced from GitHub's security advisory data feed available on the GraphQL API. The fields available in this data set are limited to those exposed in the V3 catalog and does not include all information that GitHub has about the vulnerability.

|                              |                                                                                                                           |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | One or more rows per package, more than one if the package has multiple vulnerabilities                                   |
| Child tables                 |                                                                                                                           |
| Parent tables                |                                                                                                                           |
| Column used for partitioning | Identity                                                                                                                  |
| Data file container name     | packagevulnerabilities                                                                                                    |
| Driver implementation        | [`CatalogDataToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/CatalogDataToCsv/CatalogDataToCsvDriver.cs)         |
| Record type                  | [`PackageVulnerabilityRecord`](../../src/Worker.Logic/CatalogScan/Drivers/CatalogDataToCsv/PackageVulnerabilityRecord.cs) |

## Table schema

| Column name            | Data type | Required             | Description                                                            |
| ---------------------- | --------- | -------------------- | ---------------------------------------------------------------------- |
| ScanId                 | string    | No                   | Unused, always empty                                                   |
| ScanTimestamp          | timestamp | No                   | Unused, always empty                                                   |
| LowerId                | string    | Yes                  | Lowercase package ID. Good for joins                                   |
| Identity               | string    | Yes                  | Lowercase package ID and lowercase, normalized version. Good for joins |
| Id                     | string    | Yes                  | Original case package ID                                               |
| Version                | string    | Yes                  | Original case, normalized package version                              |
| CatalogCommitTimestamp | timestamp | Yes                  | Latest catalog commit timestamp for the package                        |
| Created                | timestamp | Yes, for non-Deleted | When the package version was created                                   |
| ResultType             | enum      | Yes                  | Type of record (e.g. Vulnerable, Deleted)                              |
| GitHubDatabaseKey      | int       | Yes, for Vulnerable  | The database key in the GitHub vulnerability database                  |
| AdvisoryUrl            | string    | Yes, for Vulnerable  | The URL for reading more about the vulnerability                       |
| Severity               | int       | Yes, for Vulnerable  | The severity of the vulnerability                                      |

## ResultType schema

| Enum value    | Description                                               |
| ------------- | --------------------------------------------------------- |
| Deleted       | The package is deleted and no metadata is available       |
| NotVulnerable | The package is available and has no known vulnerabilities |
| Vulnerable    | The package is available but has known vulnerabilities    |

## Severity schema

The set of non-numeric values is defined by the [`SecurityAdvisorySeverity`](https://developer.github.com/v4/enum/securityadvisoryseverity/) enum on GitHub's GraphQL API.

| Numeric value | Description |
| ------------- | ----------- |
| 0             | Low         |
| 1             | Moderate    |
| 2             | High        |
| 3             | Critical    |
