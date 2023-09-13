# PackageLicenses

This table contains metadata about package licenses and full content of NuGet package license files. There are three types of licenses:

1. Embedded license files, which are packed into the NuGet package and are immutable per version
2. License expressions, which are immutable SPDX license expressions put into the package .nuspec
3. License URLs, which is the legacy format. Note that the previous two formats have license URLs in addition to their other license content for backwards compatibility.

Licenses are optional so some packages have none of these types.

|                              |                                                                                                                            |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.org                                                                                       |
| Child tables                 |                                                                                                                            |
| Parent tables                |                                                                                                                            |
| Column used for partitioning | Identity                                                                                                                   |
| Data file container name     | packagelicenses                                                                                                            |
| Driver implementation        | [`PackageLicenseToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageLicenseToCsv/PackageLicenseToCsvDriver.cs) |
| Record type                  | [`PackageLicense`](../../src/Worker.Logic/CatalogScan/Drivers/PackageLicenseToCsv/PackageLicense.cs)                       |

## Table schema

| Column name                   | Data type        | Required                     | Description                                                            |
| ----------------------------- | ---------------- | ---------------------------- | ---------------------------------------------------------------------- |
| ScanId                        | string           | No                           | Unused, always empty                                                   |
| ScanTimestamp                 | timestamp        | No                           | Unused, always empty                                                   |
| LowerId                       | string           | Yes                          | Lowercase package ID. Good for joins                                   |
| Identity                      | string           | Yes                          | Lowercase package ID and lowercase, normalized version. Good for joins |
| Id                            | string           | Yes                          | Original case package ID                                               |
| Version                       | string           | Yes                          | Original case, normalized package version                              |
| CatalogCommitTimestamp        | timestamp        | Yes                          | Latest catalog commit timestamp for the package                        |
| Created                       | timestamp        | Yes, for non-Deleted         | When the package version was created                                   |
| ResultType                    | enum             | Yes                          | Type of record (e.g. Deleted, None, Url, Expression, File)             |
| Url                           | string           | No                           | The license URL. Present for Expression and File result types          |
| Expression                    | string           | No                           | The license expression string                                          |
| File                          | string           | No                           | The author-provided license file name                                  |
| GeneratedUrl                  | string           | Yes, for Expression and File | The expected license URL for Expression and File licenses              |
| ExpressionParsed              | object           | Yes, for Expression          | JSON serialized license expression tree                                |
| ExpressionLicenses            | array of strings | Yes, for Expression          | Array of license identifiers (no duplicates)                           |
| ExpressionExceptions          | array of strings | Yes, for Expression          | Array of license exception identifiers (no duplicates)                 |
| ExpressionNonStandardLicenses | array of strings | Yes, for Expression          | Array of non-standard license identifiers (no duplicates)              |
| FileSize                      | long             | Yes, for File                | Size of the license file in bytes                                      |
| FileSHA256                    | string           | Yes, for File                | Base64 encoded SHA-256 of the license file                             |
| FileContent                   | string           | Yes, for File                | The full license file content                                          |

## ResultType schema

| Enum value | Description                                                |
| ---------- | ---------------------------------------------------------- |
| Deleted    | The package is deleted and no metadata is available        |
| Expression | The package has a license expression                       |
| File       | The package has an embedded license file                   |
| None       | The package has no license                                 |
| Url        | The package has a license URL but no embedded license info |

## ExpressionParsed schema

This is a recursive object model, mimicking the [`NuGetLicenseExpression`](https://github.com/NuGet/NuGet.Client/blob/11a62174beb847bc2a59ffbb08b70eaa84781d25/src/NuGet.Core/NuGet.Packaging/Licenses/NuGetLicenseExpression.cs#L7) class in NuGet.Client code, and its subclasses.

The ExpressionParsed object has the following schema.

| Property name | Data type | Required | Description                                                                     |
| ------------- | --------- | -------- | ------------------------------------------------------------------------------- |
| Type          | enum      | true     | The type of license expression, i.e the type of the root node in the expression |

The Type property enum has the following values.

| Enum value | Description                                 |
| ---------- | ------------------------------------------- |
| License    | A license and an optional plus modifier     |
| Operator   | An operator, either logical or an exception |

If the Type property is a License, the ExpressionParsed object has the following additional
properties. 

| Property name     | Data type | Required | Description                                                             |
| ----------------- | --------- | -------- | ----------------------------------------------------------------------- |
| Identifier        | string    | true     | The SPDX license identifier, like `MIT`                                 |
| Plus              | boolean   | true     | Allow later version of the license                                      |
| IsStandardLicense | boolean   | true     | Whether or not the license is considered standard by NuGet license data |

If the Type property is an Operator, the ExpressionParsed object has the following additional
properties. 

| Property name | Data type | Required | Description          |
| ------------- | --------- | -------- | -------------------- |
| OperatorType  | enum      | true     | The type of operator |

The OperatorType property enum has the following values.

| Enum value      | Description                                                   |
| --------------- | ------------------------------------------------------------- |
| WithOperator    | This expression is a `WITH` operator (for license exceptions) |
| LogicalOperator | This expression is a logical operation (`AND` or `OR`)        |

If the OperatorType property is a WithOperator, the ExpressionParsed object has the following additional
properties. 

| Property name | Data type | Required | Description                           |
| ------------- | --------- | -------- | ------------------------------------- |
| License       | object    | true     | The license that has the exception    |
| Exception     | object    | true     | The exception attached to the license |

The License property has the same type as ExpressionParsed but always has the Type of License.

The Exception property is an object that has the following schema.

| Property name | Data type | Required | Description                                   |
| ------------- | --------- | -------- | --------------------------------------------- |
| Identifier    | object    | true     | The SPDX identifier of this license exception |

If the OperatorType property is a LogicalOperator, the ExpressionParsed object has the following additional
properties. 

| Property name       | Data type | Required | Description                       |
| ------------------- | --------- | -------- | --------------------------------- |
| LogicalOperatorType | enum      | true     | The type of this logical operator |
| Left                | object    | true     | The left operand                  |
| Right               | object    | true     | The right operand                 |

The LogicalOperatorType property enum has the following values.

| Enum value | Description                                                        |
| ---------- | ------------------------------------------------------------------ |
| And        | Both the Left and Right expression must be met                     |
| Or         | Either the Left or the Right expression must be met, non-exclusive |

Both the Left and Right property are objects that have the same type as ExpressionParsed.

## ExpressionLicenses schema

This is an array of strings where each string is an SPDX license identifier, extracted from the license expression.

Duplicate identifiers are excluded.

SPDX.org has a [full list of their official license identifiers](https://spdx.org/licenses/).

## ExpressionExceptions schema

This is an array of strings where each string is an SPDX license identifier, extracted from the license expression.

Duplicate identifiers are excluded.

SPDX.org has a [full list of their official license exception identifiers](https://spdx.org/licenses/exceptions-index.html).

## ExpressionNonStandardLicenses schema

This is a subset of the ExpressionLicenses for non-standard licenses. NuGet.org should reject all of these.

Duplicate identifiers are excluded.
