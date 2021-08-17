# PackageSignatures

This table contains metadata about NuGet package signature information. It includes columns about both author and repository signatures.

|                              |                                                                                                                                  |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.org                                                                                             |
| Child tables                 |                                                                                                                                  |
| Parent tables                |                                                                                                                                  |
| Column used for partitioning | Identity                                                                                                                         |
| Data file container name     | packagesignatures                                                                                                                |
| Driver implementation        | [`PackageSignatureToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageSignatureToCsv/PackageSignatureToCsvDriver.cs) |
| Record type                  | [`PackageSignature`](../../src/Worker.Logic/CatalogScan/Drivers/PackageSignatureToCsv/PackageSignature.cs)                       |

## Table schema

| Column name                     | Data type        | Required           | Description                                                                                                  |
| ------------------------------- | ---------------- | ------------------ | ------------------------------------------------------------------------------------------------------------ |
| ScanId                          | string           | No                 | Unused, always empty                                                                                         |
| ScanTimestamp                   | timestamp        | No                 | Unused, always empty                                                                                         |
| LowerId                         | string           | Yes                | Lowercase package ID. Good for joins                                                                         |
| Identity                        | string           | Yes                | Lowercase package ID and lowercase, normalized version. Good for joins                                       |
| Id                              | string           | Yes                | Original case package ID                                                                                     |
| Version                         | string           | Yes                | Original case, normalized package version                                                                    |
| CatalogCommitTimestamp          | timestamp        | Yes                | Latest catalog commit timestamp for the package                                                              |
| Created                         | timestamp        | Yes, for Available | When the package version was created                                                                         |
| ResultType                      | enum             | Yes                | Type of record (e.g. Available, Deleted)                                                                     |
| HashAlgorithm                   | enum             | Yes                | Type of the signature hash algorithm used. Unknown is only used for Deleted packages                         |
| HashValue                       | string           | Yes, for Available | The Base64 encoded content hash. This is the package hash excluding the embedded signature file              |
| AuthorSHA1                      | string           | No                 | The SHA1 fingerprint of the author signing certificate                                                       |
| AuthorSHA256                    | string           | No                 | The SHA256 fingerprint of the author signing certificate                                                     |
| AuthorSubject                   | string           | No                 | The Subject distinguished name of the author signing certificate                                             |
| AuthorNotBefore                 | timestamp        | No                 | The start of the validity period for the author signing certificate                                          |
| AuthorNotAfter                  | timestamp        | No                 | The end of the validity period for the author signing certificate                                            |
| AuthorIssuer                    | string           | No                 | The Subject distinguished name of the direct issuer of the author signing certificate                        |
| AuthorTimestampSHA1             | string           | No                 | The SHA1 fingerprint of timestamp certificate in the author signature                                        |
| AuthorTimestampSHA256           | string           | No                 | The SHA256 fingerprint of timestamp certificate in the author signature                                      |
| AuthorTimestampSubject          | string           | No                 | The Subject distinguished name of the timestamp certificate in the author signature                          |
| AuthorTimestampNotBefore        | timestamp        | No                 | The start of the validity period for the timestamp certificate in the author signature                       |
| AuthorTimestampNotAfter         | timestamp        | No                 | The end of the validity period for the timestamp certificate in the author signature                         |
| AuthorTimestampIssuer           | string           | No                 | The Subject distinguished name of the direct issuer of the timestamp certificate in the author signature     |
| AuthorTimestampValue            | timestamp        | No                 | The value of the timestamp in the author signature                                                           |
| AuthorTimestampHasASN1Error     | bool             | Yes                | Whether the author signature timestamp has an ASN.1 error, defaults to false                                 |
| RepositorySHA1                  | string           | Yes, for Available | The SHA1 fingerprint of the repository signing certificate                                                   |
| RepositorySHA256                | string           | Yes, for Available | The SHA256 fingerprint of the repository signing certificate                                                 |
| RepositorySubject               | string           | Yes, for Available | The Subject distinguished name of the repository signing certificate                                         |
| RepositoryNotBefore             | timestamp        | Yes, for Available | The start of the validity period for the repository signing certificate                                      |
| RepositoryNotAfter              | timestamp        | Yes, for Available | The end of the validity period for the repository signing certificate                                        |
| RepositoryIssuer                | string           | Yes, for Available | The Subject distinguished name of the direct issuer of the repository signing certificate                    |
| RepositoryTimestampSHA1         | string           | Yes, for Available | The SHA1 fingerprint of timestamp certificate in the repository signature                                    |
| RepositoryTimestampSHA256       | string           | Yes, for Available | The SHA256 fingerprint of timestamp certificate in the repository signature                                  |
| RepositoryTimestampSubject      | string           | Yes, for Available | The Subject distinguished name of the timestamp certificate in the repository signature                      |
| RepositoryTimestampNotBefore    | timestamp        | Yes, for Available | The start of the validity period for the timestamp certificate in the repository signature                   |
| RepositoryTimestampNotAfter     | timestamp        | Yes, for Available | The end of the validity period for the timestamp certificate in the repository signature                     |
| RepositoryTimestampIssuer       | string           | Yes, for Available | The Subject distinguished name of the direct issuer of the timestamp certificate in the repository signature |
| RepositoryTimestampValue        | timestamp        | Yes, for Available | The value of the timestamp in the repository signature                                                       |
| RepositoryTimestampHasASN1Error | bool             | Yes                | Whether the repository signature timestamp has an ASN.1 error, defaults to false                             |
| PackageOwners                   | array of strings | No                 | The owner usernames of the package at the time of publishing                                                 |

## ResultType schema

The ResultType enum indicates the possible variants of records.

| Enum value | Description                                         |
| ---------- | --------------------------------------------------- |
| Available  | The package is available and processed successfully |
| Deleted    | The package is deleted and no metadata is available |

## HashAlgorithm schema

The HashAlgorithm enum has one of following values:

| Enum value | Description                                   |
| ---------- | --------------------------------------------- |
| SHA256     | The signature uses the SAH256 hash algorithm. |
| SHA384     | The signature uses the SHA384 hash algorithm. |
| SHA512     | The signature uses the SHA512 hash algorithm. |
| Unknown    | The signature hash algorithm is unknown.      |

## PackageOwners schema

The PackageOwners is an array of strings where each string is the username of the user or organization owning the package at the time of publishing. This value does not change over time and therefore may not reflect the current owners of the package. When the package has no owners, the value will be null, not an empty array.
