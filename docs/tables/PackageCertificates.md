# PackageCertificates

This is a mapping table between a package and all of the certificates that are used in the package signature.
Essentially the relationship between packages and certificates is many-to-many. This table describes all of the "edges"
between "package nodes" and "certificate nodes" in this graph.

|                              |                                                                                                                                        |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | One or more rows per package, more than one if the package has multiple related certificates                                           |
| Child tables                 | [Certificates](Certificates.md) joined on Fingerprint                                                                                  |
| Parent tables                |                                                                                                                                        |
| Column used for partitioning | Identity                                                                                                                               |
| Data file container name     | packagecertificates                                                                                                                    |
| Driver implementation        | [`PackageCertificateToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageCertificateToCsv/PackageCertificateToCsvDriver.cs) |
| Record type                  | [`PackageCertificateRecord`](../../src/Worker.Logic/CatalogScan/Drivers/PackageCertificateToCsv/PackageCertificateRecord.cs)           |

## Table schema

| Column name            | Data type  | Required             | Description                                                            |
| ---------------------- | ---------- | -------------------- | ---------------------------------------------------------------------- |
| ScanId                 | string     | No                   | Unused, always empty                                                   |
| ScanTimestamp          | timestamp  | No                   | Unused, always empty                                                   |
| LowerId                | string     | Yes                  | Lowercase package ID. Good for joins                                   |
| Identity               | string     | Yes                  | Lowercase package ID and lowercase, normalized version. Good for joins |
| Id                     | string     | Yes                  | Original case package ID                                               |
| Version                | string     | Yes                  | Original case, normalized package version                              |
| CatalogCommitTimestamp | timestamp  | Yes                  | Latest catalog commit timestamp for the package                        |
| Created                | timestamp  | Yes, for non-Deleted | When the package version was created                                   |
| ResultType             | enum       | Yes                  | Type of record (e.g. Available, Deleted)                               |
| Fingerprint            | string     | Yes                  | The SHA-256, base64 URL fingerprint of the related certificate         |
| RelationshipTypes      | flags enum | Yes                  | The ways that this certificate is used by this package                 |

## ResultType schema

| Enum value | Description                                         |
| ---------- | --------------------------------------------------- |
| Available  | The package is available and processed successfully |
| Deleted    | The package is deleted and no metadata is available |

## RelationshipTypes schema

| Enum value                           | Description                                                                                                                           |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------- |
| AuthorCodeSigningChainContains       | This certificate is in the chain of the author signing certificate.                                                                   |
| AuthorTimestampingChainContains      | This certificate is in the chain of the timestamping certificate for the author signature.                                            |
| AuthorTimestampSignedCmsContains     | The signed CMS of the author signature timestamp contains this certificate.                                                           |
| IsAuthorCodeSignedBy                 | This certificate is the end certificate for an author signature.                                                                      |
| IsAuthorTimestampedBy                | This certificate is the end certificate for timestamping the author signature.                                                        |
| IsRepositoryCodeSignedBy             | This certificate is the end certificate for an repository signature.                                                                  |
| IsRepositoryTimestampedBy            | This certificate is the end certificate for timestamping the repository signature.                                                    |
| None                                 | Unused. If a package has not relationship with a certificate, no row will be present in the table.                                    |
| PrimarySignedCmsContains             | The primary signed CMS contains this certificate. Both author and repository code signing chains should be in the primary signed CMS. |
| RepositoryCodeSigningChainContains   | This certificate is in the chain of the repository signing certificate.                                                               |
| RepositoryTimestampingChainContains  | This certificate is in the chain of the timestamping certificate for the repository signature.                                        |
| RepositoryTimestampSignedCmsContains | The signed CMS of the repository signature timestamp contains this certificate.                                                       |
