# Certificates

This table contains certificate metadata and validation results (for end certificates) for all certificates referenced
in any package signature. This includes code signing and timestamp certificate chains as well as the author and
repository signature.

|                              |                                                                                                                                        |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | One row per certificate used in a package signature chain                                                                              |
| Child tables                 |                                                                                                                                        |
| Parent tables                | [PackageCertificates](PackageCertificates.md) joined on Fingerprint                                                                    |
| Column used for partitioning | Fingerprint                                                                                                                            |
| Data file container name     | certificates                                                                                                                           |
| Driver implementation        | [`PackageCertificateToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageCertificateToCsv/PackageCertificateToCsvDriver.cs) |
| Record type                  | [`CertificateRecord`](../../src/Worker.Logic/CatalogScan/Drivers/PackageCertificateToCsv/CertificateRecord.cs)                         |

## Table schema

| Column name                  | Data type        | Required | Description                                                                                                              |
| ---------------------------- | ---------------- | -------- | ------------------------------------------------------------------------------------------------------------------------ |
| ScanId                       | string           | No       | Unused, always empty                                                                                                     |
| ScanTimestamp                | timestamp        | No       | Unused, always empty                                                                                                     |
| ResultType                   | enum             | No       | Always Available, unused certificates are removed from the table                                                         |
| Fingerprint                  | string           | Yes      | SHA-256, base64 URL fingerprint of the certificate                                                                       |
| FingerprintSHA256Hex         | string           | Yes      | SHA-256, hex fingerprint                                                                                                 |
| FingerprintSHA1Hex           | string           | Yes      | SHA-1, hex fingerprint                                                                                                   |
| Subject                      | string           | Yes      | The subject distinguished name                                                                                           |
| Issuer                       | string           | Yes      | The issuer distinguished name                                                                                            |
| NotBefore                    | timestamp        | Yes      | The start of the validity period                                                                                         |
| NotAfter                     | timestamp        | Yes      | The end of the validity period                                                                                           |
| SerialNumber                 | string           | Yes      | The serial number of the certificate                                                                                     |
| SignatureAlgorithmOid        | string           | Yes      | The OID of the signature algorithm used in the certificate                                                               |
| Version                      | int              | Yes      | The X.509 version                                                                                                        |
| Extensions                   | array of objects | Yes      | All X.509 certificate extensions and recognized metadata                                                                 |
| PublicKeyOid                 | string           | Yes      | The OID of the public key type                                                                                           |
| RawDataLength                | int              | Yes      | The length of the certificate data in bytes                                                                              |
| RawData                      | string           | Yes      | Base64 encoded certificate data                                                                                          |
| IssuerFingerprint            | string           | No       | SHA-256, base64 URL fingerprint of the issuer certificate, useful for joining                                            |
| RootFingerprint              | string           | No       | SHA-256, base64 URL fingerprint of the root certificate, useful for joining                                              |
| ChainLength                  | int              | No       | The number of certificates in the chain, including the end and root certificate                                          |
| CodeSigningCommitTimestamp   | timestamp        | No       | The latest catalog commit timestamp that caused this code signing verification result to be created                      |
| CodeSigningStatus            | enum             | No       | Status of the code signing verification result                                                                           |
| CodeSigningStatusFlags       | flags enum       | No       | Flattened flags for chain found by code signing verification                                                             |
| CodeSigningStatusUpdateTime  | timestamp        | No       | The time that the end certificate's status was last updated, according to the CA, found during code signing verification |
| CodeSigningRevocationTime    | timestamp        | No       | The time at which the end certificate was revoked, found during to code signing verification                             |
| TimestampingCommitTimestamp  | timestamp        | No       | The latest catalog commit timestamp that caused this timestamping verification result to be created                      |
| TimestampingStatus           | enum             | No       | Status of the timestamping verification result                                                                           |
| TimestampingStatusFlags      | flags enum       | No       | Flattened flags for chain found by timestamping verification                                                             |
| TimestampingStatusUpdateTime | timestamp        | No       | The time that the end certificate's status was last updated, according to the CA, found during timestamping verification |
| TimestampingRevocationTime   | timestamp        | No       | The time at which the end certificate was revoked, found during to timestamping verification                             |

## ResultType schema

The ResultType enum indicates the possible variants of records. Only Available is used currently.

| Enum value | Description                                                                             |
| ---------- | --------------------------------------------------------------------------------------- |
| Available  | The certificate is used by at least one package                                         |
| Deleted    | The certificate is not used by any package and will therefore be removed from the table |

## Extensions schema

The Extensions field is an array of objects. Each object has the following schema.

| Property name | Data type | Required | Description                                                                   |
| ------------- | --------- | -------- | ----------------------------------------------------------------------------- |
| Oid           | string    | true     | The object ID (OID) of the extension                                          |
| Critical      | bool      | true     | Whether or not the extension should be rejected if unrecognized (no observed) |
| Recognized    | bool      | true     | Whether or not the extension is recognized and has additional properties      |
| RawDataLength | int       | true     | The length in bytes of the raw data for the extension                         |
| RawData       | string    | true     | Base64 encoding of the raw data for the extension                             |

When the Oid property is `2.5.29.19` ("basic constraints"), the object has the following additional properties. This is
a projection of [`X509BasicConstraintsExtension`](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509basicconstraintsextension).

| Property name           | Data type | Required | Description                                                                    |
| ----------------------- | --------- | -------- | ------------------------------------------------------------------------------ |
| CertificateAuthority    | bool      | true     | Whether a certificate is a certificate authority (CA) certificate              |
| HasPathLengthConstraint | bool      | true     | Whether a certificate has a restriction on the number of path levels it allows |
| PathLengthConstraint    | bool      | true     | The number of levels allowed in a certificate's path                           |

When the Oid property is `2.5.29.15` ("key usage"), the object has the following additional properties. This is
a projection of [`X509KeyUsageExtension`](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509keyusageextension).

| Property name | Data type  | Required | Description                                        |
| ------------- | ---------- | -------- | -------------------------------------------------- |
| KeyUsages     | flags enum | true     | The key usage flag associated with the certificate |

The KeyUsages is the [`X509KeyUsageExtension.KeyUsages`](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509keyusageextension.keyusages) flags enum.

When the Oid property is `2.5.29.37` ("enhanced key usage"), the object has the following additional properties. This is
a projection of [`X509EnhancedKeyUsageExtension`](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509enhancedkeyusageextension).

| Property name        | Data type        | Required | Description                                                                                 |
| -------------------- | ---------------- | -------- | ------------------------------------------------------------------------------------------- |
| EnhancedKeyUsageOids | array of strings | true     | The collection of object identifiers (OIDs) that indicate the applications that use the key |

When the Oid property is `2.5.29.14` ("subject key identifier"), the object has the following additional properties. This is
a projection of [`X509SubjectKeyIdentifierExtension`](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509subjectkeyidentifierextension).

| Property name        | Data type | Required | Description                                                   |
| -------------------- | --------- | -------- | ------------------------------------------------------------- |
| SubjectKeyIdentifier | string    | true     | Represents the subject key identifier (SKI) for a certificate |

## CodeSigningStatus schema

This is the `EndCertificateStatus` found in [NuGet/ServerCommon](https://github.com/NuGet/ServerCommon/blob/6e7b563c5d5a03ed4f0918defda64eeecbcadeb9/src/NuGet.Services.Validation/Entities/EndCertificateStatus.cs)

| Enum value | Description                                                                                                                                                                                                       |
| ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Good       | The end certificate is valid and has not been revoked.                                                                                                                                                            |
| Invalid    | The end certificate has failed offline validations. This could be for a number of reasons including an untrusted root or weak hashing algorithm. Anything signed by the certificate should be considered invalid. |
| Revoked    | The end certificate has been revoked by the certificate authority. Anything signed by the certificate after end certificate revocation time should be considered invalid.                                         |
| Unknown    | The status is unknown if this end certificate's online verification has never completed.                                                                                                                          |

## CodeSigningStatusFlags schema

This is the [`X509ChainStatusFlags`](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509chainstatusflags) enum found in
.NET.

<!-- NO TABLE -->

## TimestampingStatus schema

Same as the [CodeSigningStatus schema](#codesigningstatus-schema).

<!-- NO TABLE -->

## TimestampingStatusFlags schema

Same as the [CodeSigningStatusFlags schema](#codesigningstatusflags-schema).

<!-- NO TABLE -->
