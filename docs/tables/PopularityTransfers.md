# PopularityTransfers

This table contains the set of current popularity transfers for the package ID. This table contains no historical data.

The popularity transfer feature on NuGet.org is an optional feature that package owners can use to transfer package
downloads from an old, deprecated package to a newer replacement package. For more details see the [Transfer popularity
to a newer
package](https://learn.microsoft.com/en-us/nuget/nuget-org/deprecate-packages#transfer-popularity-to-a-newer-package)
document.

|                              |                                                                                                                                           |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per unique package ID on NuGet.org, even if the package has no popularity transfers                                           |
| Child tables                 |                                                                                                                                           |
| Parent tables                |                                                                                                                                           |
| Column used for partitioning | LowerId                                                                                                                                   |
| Data file container name     | popularitytransfers                                                                                                                       |
| Driver                       | [`PopularityTransfersToCsvUpdater`](../../src/Worker.Logic/MessageProcessors/PopularityTransfersToCsv/PopularityTransfersToCsvUpdater.cs) |
| Record type                  | [`PopularityTransfersRecord`](../../src/Worker.Logic/MessageProcessors/PopularityTransfersToCsv/PopularityTransfersRecord.cs)             |

## Table schema

| Column name      | Data type        | Required | Description                                                                                         |
| ---------------- | ---------------- | -------- | --------------------------------------------------------------------------------------------------- |
| AsOfTimestamp    | timestamp        | No       | Unused, always empty                                                                                |
| LowerId          | string           | Yes      | Lowercase package ID. Good for joins                                                                |
| Id               | string           | Yes      | Original case package ID for an arbitrary package version                                           |
| TransferIds      | array of strings | Yes      | Zero or more package IDs that receive the popularity from the package identified by the `Id` column |
| TransferLowerIds | array of strings | Yes      | Same as `TransferIds` but all IDs are lowercased (for easier joining)                               |

## TransferIds schema

The TransferIds column is an array of strings where each string is a package ID of the package that will receive all or
a portion of the transferred popularity. This array can be empty, meaning the package has no popularity transfers.

## TransferLowerIds schema

This is exactly the same as the TransferIds column except the package IDs are lowercased.
