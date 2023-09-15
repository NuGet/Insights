# PackageSignatureToCsv

This driver extracts data from each package signature (`.signature.p7s` file) on NuGet.org.

|                                    |                                                                                                                                                                                                  |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `CatalogScanDriverType` enum value | `PackageSignatureToCsv`                                                                                                                                                                          |
| Driver implementation              | [`PackageSignatureToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageSignatureToCsv/PackageSignatureToCsvDriver.cs)                                                                 |
| Processing mode                    | process latest catalog leaf per package ID and version                                                                                                                                           |
| Cursor dependencies                | [`LoadPackageArchive`](LoadPackageArchive.md): reads package signature bytes from table storage                                                                                                  |
| Components using driver output     | Kusto ingestion via [`KustoIngestionMessageProcessor`](../../src/Worker.Logic/MessageProcessors/KustoIngestion/KustoIngestionMessageProcessor.cs), since this driver produces CSV data           |
| Temporary storage config           | Table Storage:<br />`CsvRecordTableName` (name prefix): holds CSV records before they are added to a CSV blob<br />`TaskStateTableName` (name prefix): tracks completion of CSV blob aggregation |
| Persistent storage config          | Blob Storage:<br />`PackageSignatureContainerName`: contains CSVs for the [`PackageSignatures`](../tables/PackageSignatures.md) table                                                            |
| Output CSV tables                  | [`PackageSignatures`](../tables/PackageSignatures.md)                                                                                                                                            |

## Algorithm

For each catalog leaf, the package signature bytes are fetched from Azure Table Storage (as stored by [`LoadPackageArchive`](LoadPackageArchive.md)). The signature bytes are read using NuGet client APIs like [`PrimarySignature`](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/Signing/Signatures/PrimarySignature.cs) and [`RepositoryCountersignature`](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/Signing/Signatures/RepositoryCountersignature.cs). The signature file is the `.signature.p7s` file found in the root of the package .nupkg.

Some metadata about signing certificates is extracted to the output CSV record and is redundant with the certificate relationships enumerated by the [`PackageCertificateToCsv`](PackageCertificateToCsv.md) driver. Other metadata is not directly related to certificates but specific to the signature itself, for example the list of package owners at signing time in the repository signature or the package content hash.

All of the signature and certificate information found in the package signature is saved to a CSV record.
