# PackageAssemblies

This table contains metadata about all assemblies files on NuGet.org. The analysis and produced columns are focused on .NET assembly metadata.
For simplicity, only files with the `.dll` or `.exe` file extension are analyzed.

|                              |                                                                                                         |
| ---------------------------- | ------------------------------------------------------------------------------------------------------- |
| Cardinality                  | One or more rows per package, more than one if the package has multiple assemblies                      |
| Child tables                 |                                                                                                         |
| Parent tables                |                                                                                                         |
| Column used for partitioning | Identity                                                                                                |
| Data file container name     | packageassemblies                                                                                       |
| Driver                       | [`PackageAssemblyToCsv`](../drivers/PackageAssemblyToCsv.md)                                            |
| Record type                  | [`PackageAssembly`](../../src/Worker.Logic/CatalogScan/Drivers/PackageAssemblyToCsv/PackageAssembly.cs) |

## Table schema

| Column name                     | Data type        | Required                   | Description                                                            |
| ------------------------------- | ---------------- | -------------------------- | ---------------------------------------------------------------------- |
| ScanId                          | string           | No                         | Unused, always empty                                                   |
| ScanTimestamp                   | timestamp        | No                         | Unused, always empty                                                   |
| LowerId                         | string           | Yes                        | Lowercase package ID. Good for joins                                   |
| Identity                        | string           | Yes                        | Lowercase package ID and lowercase, normalized version. Good for joins |
| Id                              | string           | Yes                        | Original case package ID                                               |
| Version                         | string           | Yes                        | Original case, normalized package version                              |
| CatalogCommitTimestamp          | timestamp        | Yes                        | Latest catalog commit timestamp for the package                        |
| Created                         | timestamp        | Yes, for non-Deleted       | When the package version was created                                   |
| ResultType                      | enum             | Yes                        | Type of record (e.g. Available, Deleted)                               |
| Path                            | string           | Yes, for ZIP entries       | The relative file path within the .nupkg                               |
| FileName                        | string           | Yes, for ZIP entries       | The file name from the Path                                            |
| FileExtension                   | string           | Yes, for ZIP entries       | The file extension from the Path                                       |
| TopLevelFolder                  | string           | Yes, for ZIP entries       | The first folder (i.e. directory) name from the Path                   |
| CompressedLength                | long             | Yes, for ZIP entries       | The compressed size of the assembly                                    |
| EntryUncompressedLength         | long             | Yes, for ZIP entries       | The uncompressed size of the assembly                                  |
| ActualUncompressedLength        | long             | Yes, for valid ZIP entries | The uncompressed size of the assembly                                  |
| FileSHA256                      | string           | Yes, for valid ZIP entries | The Base64 encoded SHA256 hash of the assembly file                    |
| EdgeCases                       | flags enum       | Yes, for ValidAssembly     | Edges cases or errors encountered while processing the assembly        |
| AssemblyName                    | string           | Yes, for ValidAssembly     | The .NET assembly Name                                                 |
| AssemblyVersion                 | string           | Yes, for ValidAssembly     | The .NET assembly version                                              |
| Culture                         | string           | No                         | The culture of the .NET assembly                                       |
| PublicKeyToken                  | string           | No                         | The public key token for .NET assembly strong naming                   |
| HashAlgorithm                   | enum             | Yes, for ValidAssembly     | The hash algorithm enum for the assembly                               |
| HasPublicKey                    | bool             | Yes, for ValidAssembly     | Whether or not the .NET assembly has a public key                      |
| PublicKeyLength                 | int              | No                         | The length in bytes of the public key                                  |
| PublicKeySHA1                   | string           | No                         | The Base64 encoding SHA1 hash of the public key                        |
| CustomAttributes                | object           | No                         | Assembly custom attribute names and parameters                         |
| CustomAttributesFailedDecode    | array of strings | No                         | Custom attribute names that failed to decode                           |
| CustomAttributesTotalCount      | int              | No                         | Total number of assembly custom attributes                             |
| CustomAttributesTotalDataLength | int              | No                         | Total size of custom attribute value blobs in bytes                    |

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

## EdgeCases schema

The EdgeCases column is a enum with one or more of any of the following values. It is formatted by separating the values by a comma and a space.

| Enum value                                  | Description                                                                            |
| ------------------------------------------- | -------------------------------------------------------------------------------------- |
| CustomAttributes_ArrayOutOfMemory           | An argument array was too large to be allocated                                        |
| CustomAttributes_BrokenMethodDefinitionName | A reference to method definition name is broken                                        |
| CustomAttributes_BrokenValueBlob            | A reference to a custom attribute value blob is broken                                 |
| CustomAttributes_DuplicateArgumentName      | Multiple instances of the same argument name exist in a custom attribute               |
| CustomAttributes_MethodDefinition           | A custom attribute type refers to a method instead of type declaration                 |
| CustomAttributes_TruncatedAttributes        | CustomAttributes column is truncated since it exceeded a limit                         |
| CustomAttributes_TruncatedFailedDecode      | CustomAttributesFailedDecode column is truncated since it exceeded a limit             |
| CustomAttributes_TypeDefinitionConstructor  | A custom attribute constructor refers to a type definition instead of type declaration |
| Name_CultureNotFoundException               | The culture in the .NET assembly is unrecognized                                       |
| Name_FileLoadException                      | Reading the assembly name failed with a file load exception                            |
| None                                        | None edge cases were encountered                                                       |
| PublicKeyToken_Security                     | Reading the public key token threw a security exception                                |

## HashAlgorithm schema

The HashAlgorithm enum is the .NET [System.Reflection.HashAlgorithm](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.assemblyname.hashalgorithm) type.

| Enum value |
| ---------- |
| MD5        |
| None       |
| Sha1       |
| Sha256     |
| Sha384     |
| Sha512     |

## CustomAttributes schema

The CustomAttributes column is a JSON encoded object. Each key is the name of the custom attribute without namespace and with any `Attribute` suffix removed, for brevity. Each value of the outermost object is an array of object, where each inner object contains the attribute arguments. The keys of attribute argument object are the index of a positional arguments (string representation of decimal integers) or the name of the named argument. The values in these inner objects are the argument values. There can be multiple attributes with the same name, therefore the argument objects are stored in an array per attribute name.

For example, here is one example CustomAttributes value, with JSON formatting applied:

```json
{
  "CompilationRelaxations": [
    {
      "0": 8
    }
  ],
  "RuntimeCompatibility": [
    {
      "WrapNonExceptionThrows": true
    }
  ],
  "Debuggable": [
    {
      "0": 263
    }
  ],
  "TargetFramework": [
    {
      "0": ".NETStandard,Version=v2.1",
      "FrameworkDisplayName": ""
    }
  ],
  "AssemblyMetadata": [
    {
      "0": "RepositoryUrl",
      "1": "https://github.com/NuGet/NuGetGallery"
    }
  ]
}
```

## CustomAttributesFailedDecode schema

The CustomAttributesFailedDecode column is a array of all custom attribute names that failed to decode. Similar to the keys in the CustomAttributes column, the names contained in this array are without namespace and with any `Attribute` suffix removed, for brevity.
