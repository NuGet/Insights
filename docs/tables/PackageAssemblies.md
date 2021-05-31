# PackageAssemblies

This table contains metadata about all assemblies files on NuGet.org. The analysis and produced columns are focused on .NET assembly metadata.
For simplicity, only files with the `.dll` or `.exe` file extension are analyzed.

|                              |                                                                                                                               |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | One or more rows per package, more than one if the package has multiple assemblies                                            |
| Child tables                 |                                                                                                                               |
| Parent tables                |                                                                                                                               |
| Column used for partitioning | Identity                                                                                                                      |
| Data file container name     | packageassemblies                                                                                                             |
| Driver implementation        | [`PackageAssemblyToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageAssemblyToCsv/PackageAssemblyToCsvDriver.cs) |
| Record type                  | [`PackageAssembly`](../../src/Worker.Logic/CatalogScan/Drivers/PackageAssemblyToCsv/PackageAssembly.cs)                       |

## Table schema

| Column name                             | Data type | Required                   | Description                                                            |
| --------------------------------------- | --------- | -------------------------- | ---------------------------------------------------------------------- |
| ScanId                                  | string    | No                         | Unused, always empty                                                   |
| ScanTimestamp                           | timestamp | No                         | Unused, always empty                                                   |
| LowerId                                 | string    | Yes                        | Lowercase package ID. Good for joins                                   |
| Identity                                | string    | Yes                        | Lowercase package ID and lowercase, normalized version. Good for joins |
| Id                                      | string    | Yes                        | Original case package ID                                               |
| Version                                 | string    | Yes                        | Original case, normalized package version                              |
| CatalogCommitTimestamp                  | timestamp | Yes                        | Latest catalog commit timestamp for the package                        |
| Created                                 | timestamp | Yes, for non-Deleted       | When the package version was created                                   |
| ResultType                              | enum      | Yes                        | Type of record (e.g. Available, Deleted)                               |
| Path                                    | string    | Yes, for ZIP entries       | The relative file path within the .nupkg                               |
| FileName                                | string    | Yes, for ZIP entries       | The file name from the Path                                            |
| FileExtension                           | string    | Yes, for ZIP entries       | The file extension from the Path                                       |
| TopLevelFolder                          | string    | Yes, for ZIP entries       | The first folder (i.e. directory) name from the Path                   |
| CompressedLength                        | long      | Yes, for ZIP entries       | The compressed size of the assembly                                    |
| EntryUncompressedLength                 | long      | Yes, for ZIP entries       | The uncompressed size of the assembly                                  |
| ActualUncompressedLength                | long      | Yes, for valid ZIP entries | The uncompressed size of the assembly                                  |
| FileSHA256                              | long      | Yes, for valid ZIP entries | The Base64 encoded SHA256 hash of the assembly file                    |
| HasException                            | bool      | Yes, for ValidAssembly     | Whether an exception was thrown while reading .NET assembly metadata   |
| AssemblyName                            | string    | Yes, for ValidAssembly     | The .NET assembly Name                                                 |
| AssemblyVersion                         | string    | Yes, for ValidAssembly     | The .NET assembly version                                              |
| Culture                                 | string    | No                         | The culture of the .NET assembly                                       |
| AssemblyNameHasCultureNotFoundException | bool      | No                         | The culture in the .NET assembly is unrecognized                       |
| AssemblyNameHasFileLoadException        | bool      | No                         | Reading the assembly name failed with a file load exception            |
| PublicKeyToken                          | bool      | No                         | The public key token for .NET assembly strong naming                   |
| PublicKeyTokenHasSecurityException      | bool      | No                         | Reading the public key token threw a security exception                |
| HashAlgorithm                           | enum      | Yes, for ValidAssembly     | The hash algorithm enum for the assembly                               |
| HasPublicKey                            | bool      | Yes, for ValidAssembly     | Whether or not the .NET assembly has a public key                      |
| PublicKeyLength                         | int       | No                         | The length in bytes of the public key                                  |
| PublicKeySHA1                           | string    | No                         | The Base64 encoding SHA1 hash of the public key                        |

Records are referred to as "ZIP entries" in the table above if it does not have ResultType `NoAssemblies` or `Deleted`.

Records are referred to as "valid ZIP entries" in the table above if it does not have ResultType `NoAssemblies`, `Deleted`, or `InvalidZipEntry`.

## ResultType schema

The ResultType enum indicates the possible variants of records.

| Enum value             | Description                                                         |
| ---------------------- | ------------------------------------------------------------------- |
| Deleted                | The package is deleted and no metadata is available                 |
| DoesNotContainAssembly | The assembly could not be analyzed due to no assembly content found |
| InvalidZipEntry        | The assembly could not be analyzed due to an error in the ZIP entry |
| NoAssemblies           | The package has no assemblies                                       |
| NoManagedMetadata      | The assembly could not be analyzed due to missing managed metadata  |
| NotManagedAssembly     | The record is about an unmanaged assembly                           |
| ValidAssembly          | The record is about a valid .NET assembly                           |

## HashAlgorithm enum

The HashAlgorithm enum is the .NET [System.Reflection.HashAlgorithm](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.assemblyname.hashalgorithm) type.
