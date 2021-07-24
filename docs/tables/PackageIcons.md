# PackageIcons

This table contains metadata about package icons available on NuGet.org. This contains information about both embedded
icons and icon URL icons downloaded to NuGet.org. The processing of images is done using [Magick.NET](https://github.com/dlemstra/Magick.NET).

|                              |                                                                                                                   |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| Cardinality                  | Exactly one per package on NuGet.org                                                                              |
| Child tables                 |                                                                                                                   |
| Parent tables                |                                                                                                                   |
| Column used for partitioning | Identity                                                                                                          |
| Data file container name     | packageicons                                                                                                      |
| Driver implementation        | [`PackageIconToCsvDriver`](../../src/Worker.Logic/CatalogScan/Drivers/PackageIconToCsv/PackageIconToCsvDriver.cs) |
| Record type                  | [`PackageIcon`](../../src/Worker.Logic/CatalogScan/Drivers/PackageIconToCsv/PackageIcon.cs)                       |

## Table schema

| Column name            | Data type        | Required             | Description                                                                      |
| ---------------------- | ---------------- | -------------------- | -------------------------------------------------------------------------------- |
| ScanId                 | string           | No                   | Unused, always empty                                                             |
| ScanTimestamp          | timestamp        | No                   | Unused, always empty                                                             |
| LowerId                | string           | Yes                  | Lowercase package ID. Good for joins                                             |
| Identity               | string           | Yes                  | Lowercase package ID and lowercase, normalized version. Good for joins           |
| Id                     | string           | Yes                  | Original case package ID                                                         |
| Version                | string           | Yes                  | Original case, normalized package version                                        |
| CatalogCommitTimestamp | timestamp        | Yes                  | Latest catalog commit timestamp for the package                                  |
| Created                | timestamp        | Yes, for non-Deleted | When the package version was created                                             |
| ResultType             | enum             | Yes                  | Type of record (e.g. Available, Deleted)                                         |
| FileSize               | long             | Yes, for existing    | Size of the image file in bytes                                                  |
| MD5                    | string           | Yes, for existing    | Base64 encoded MD5 hash of the image bytes                                       |
| SHA1                   | string           | Yes, for existing    | Base64 encoded SHA1 hash of the image bytes                                      |
| SHA256                 | string           | Yes, for existing    | Base64 encoded SHA256 hash of the image bytes                                    |
| SHA512                 | string           | Yes, for existing    | Base64 encodedSHA512 hash of the image bytes                                     |
| ContentType            | string           | No                   | Content-Type response header on the image file, if present                       |
| HeaderFormat           | string           | Yes, for existing    | String representing image format detected from header bytes, e.g. "Png" or "Gif" |
| AutoDetectedFormat     | bool             | Yes, for Available   | Whether Magick.NET could automatically detect the image format from bytes        |
| Signature              | string           | Yes, for Available   | Base64 encoded hash of the pixel data, independent from image format/codec       |
| Width                  | int              | Yes, for Available   | Width of the image in pixels                                                     |
| Height                 | int              | Yes, for Available   | Height of the image in pixels                                                    |
| FrameCount             | int              | Yes, for Available   | Number of frames in an animated image, or one with multiple resolutions          |
| IsOpaque               | bool             | Yes, for Available   | Whether or not all pixels are opaque (no transparency)                           |
| FrameFormats           | array of strings | Yes, for Available   | Unique formats found in image frames, same order as frames                       |
| FrameDimensions        | array of objects | Yes, for Available   | Unique dimensions found in image frames, same order as frames                    |
| FrameAttributeNames    | array of strings | Yes, for Available   | Unique attribute names found in image frames, in lex order                       |

## ResultType schema

| Enum value | Description                                                   |
| ---------- | ------------------------------------------------------------- |
| Available  | The icon is available and was opened successfully             |
| Deleted    | The package is deleted and no metadata is available           |
| Error      | The icon is available but could not be opened with Magick.NET |
| NoIcon     | The package exists but no icon is available                   |

## FrameFormats schema

The FrameFormats column is an array of strings. It is a set (unique values only) in the order that the image format was first found in an image frame. The values are Magick.NET format names like "Png" or "Gif", similar to the HeaderFormat column.

## FrameDimensions schema

The FrameFormats column is an array of object. It is a set (unique objects only) in the order that the dimensions was first found in an image frame. Each object has the following schema.

| Property name | Data type | Required | Description                   |
| ------------- | --------- | -------- | ----------------------------- |
| Width         | int       | true     | Width of the frame in pixels  |
| Height        | int       | true     | Height of the frame in pixels |

## FrameAttributeNames schema

The FrameAttributeNames column is an array of strings. It is a set (unique values only) in lex order. Each string is the name (key) of an attribute that ImageMagick discovered in an image frame. They are often in the format of `{category}:{name}`.
