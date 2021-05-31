# PackageDownloads

This table contains the current total download count for packages. This table contains no historical data.

|                              |                                                                                                             |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.org, even if the package has zero downloads                                |
| Child tables                 |                                                                                                             |
| Parent tables                |                                                                                                             |
| Column used for partitioning | Identity                                                                                                    |
| Data file container name     | packagedownloads                                                                                            |
| Driver implementation        | [`DownloadsToCsvUpdater`](../../src/Worker.Logic/MessageProcessors/DownloadsToCsv/DownloadsToCsvUpdater.cs) |
| Record type                  | [`PackageDownloadRecord`](../../src/Worker.Logic/MessageProcessors/DownloadsToCsv/PackageDownloadRecord.cs) |

## Table schema

| Column name    | Data type | Required | Description                                                            |
| -------------- | --------- | -------- | ---------------------------------------------------------------------- |
| AsOfTimestamp  | timestamp | No       | Unused, always empty                                                   |
| LowerId        | string    | Yes      | Lowercase package ID. Good for joins                                   |
| Identity       | string    | Yes      | Lowercase package ID and lowercase, normalized version. Good for joins |
| Id             | string    | Yes      | Arbitrary package ID case                                              |
| Version        | string    | Yes      | Arbitrary case, normalized package version                             |
| Downloads      | long      | Yes      | Total package downloads for this package version                       |
| TotalDownloads | long      | Yes      | Total package downloads for all versions of this package ID            |
