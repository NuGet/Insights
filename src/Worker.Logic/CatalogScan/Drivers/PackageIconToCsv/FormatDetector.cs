// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Buffers;
using CommunityToolkit.HighPerformance;
using ImageMagick;

namespace NuGet.Insights.Worker.PackageIconToCsv
{
    /// <summary>
    /// Based on https://github.com/NuGet/NuGet.Jobs/blob/e71506a0f9b2d90c44c2aa7a357a559450d89847/src/Catalog/Icons/IconProcessor.cs.
    /// </summary>
    public static class FormatDetector
    {
        /// <summary>
        /// The PNG file header bytes. All PNG files are expected to have those at the beginning of the file.
        /// </summary>
        /// <remarks>
        /// https://www.w3.org/TR/PNG/#5PNG-file-signature
        /// </remarks>
        private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>
        /// The JPG file header bytes.
        /// </summary>
        /// <remarks>
        /// Technically, JPEG start with two byte SOI (start of image) segment: FFD8, followed by several other segments or fill bytes.
        /// All of the segments start with FF, and fill bytes are FF, so we check the first 3 bytes instead of the first two.
        /// https://www.w3.org/Graphics/JPEG/itu-t81.pdf "B.1.1.2 Markers"
        /// </remarks>
        private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF };

        /// <summary>
        /// The GIF87a file header.
        /// </summary>
        /// <remarks>
        /// https://www.w3.org/Graphics/GIF/spec-gif87.txt
        /// </remarks>
        private static readonly byte[] Gif87aHeader = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 };

        /// <summary>
        /// The GIF89a file header.
        /// </summary>
        /// <remarks>
        /// https://www.w3.org/Graphics/GIF/spec-gif89a.txt
        /// </remarks>
        private static readonly byte[] Gif89aHeader = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };

        /// <summary>
        /// The .ico file "header".
        /// </summary>
        /// <remarks>
        /// This is the first 4 bytes of the ICONDIR structure expected for .ico files
        /// https://docs.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)
        /// </remarks>
        private static readonly byte[] IcoHeader = new byte[] { 0x00, 0x00, 0x01, 0x00 };

        public static MagickFormat Detect(Stream stream)
        {
            var pool = ArrayPool<byte>.Shared;
            const int length = 8192;
            var buffer = pool.Rent(length);
            try
            {
                var read = stream.Read(buffer, 0, length);
                var imageData = new ReadOnlyMemory<byte>(buffer, 0, read);
                return Detect(imageData);
            }
            finally
            {
                pool.Return(buffer);
            }
        }

        public static MagickFormat Detect(ReadOnlyMemory<byte> imageData)
        {
            // checks are ordered by format popularity among external icons for existing packages
            if (imageData.Span.StartsWith(PngHeader))
            {
                return MagickFormat.Png;
            }

            if (imageData.Span.StartsWith(JpegHeader))
            {
                return MagickFormat.Jpeg;
            }

            if (imageData.Span.StartsWith(IcoHeader))
            {
                return MagickFormat.Ico;
            }

            if (imageData.Span.StartsWith(Gif89aHeader))
            {
                return MagickFormat.Gif;
            }

            if (imageData.Span.StartsWith(Gif87aHeader))
            {
                return MagickFormat.Gif87;
            }

            if (IsSvgData(imageData))
            {
                return MagickFormat.Svg;
            }

            return MagickFormat.Unknown;
        }

        private static bool IsSvgData(ReadOnlyMemory<byte> imageData)
        {
            using (var memoryStream = imageData.AsStream())
            using (var reader = new StreamReader(memoryStream))
            {
                var stringContent = reader.ReadToEnd();
                return stringContent.Contains("<svg", StringComparison.Ordinal);
            }
        }
    }
}
