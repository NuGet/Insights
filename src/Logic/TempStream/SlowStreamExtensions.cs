// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights
{
    public static class SlowStreamExtensions
    {
        private static readonly ReadOnlyMemory<byte> OneByte = new ReadOnlyMemory<byte>(new[] { (byte)0 });

        public static async Task CopyToSlowAsync(
            this Stream src,
            Stream dest,
            long length,
            int bufferSize,
            IIncrementalHash hashAlgorithm,
            ILogger logger)
        {
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(bufferSize);
            try
            {
                long copiedBytes = 0;
                double previousPercent = -1;
                while (true)
                {
                    // Spend up to 5 second trying to fill up the buffer. This results is less chattiness on the "write" side
                    // of the copy operation. We pass the buffer size here because we want to observe the provided buffer size.
                    // The memory pool that gave us the buffer may have given us a buffer larger than the provided buffer size.
                    var bytesRead = await ReadForDurationAsync(src, buffer, bufferSize, TimeSpan.FromSeconds(5));

                    copiedBytes += bytesRead;
                    var percent = 1.0 * copiedBytes / length;
                    var logLevel = bytesRead == 0 || (int)(percent * 100) != (int)(previousPercent * 100) ? LogLevel.Information : LogLevel.Debug;
                    logger.Log(logLevel, "Read {BufferBytes} bytes ({CopiedBytes} of {TotalBytes}, {Percent:P2}).", bytesRead, copiedBytes, length, percent);

                    if (bytesRead == 0)
                    {
                        hashAlgorithm.TransformFinalBlock();
                        break;
                    }

                    hashAlgorithm.TransformBlock(buffer, 0, bytesRead);

                    await dest.WriteAsync(buffer, 0, bytesRead);
                    logger.Log(logLevel, "Wrote {BufferBytes} bytes ({CopiedBytes} of {TotalBytes}, {Percent:P2}).", bytesRead, copiedBytes, length, percent);
                    previousPercent = percent;
                }

                if (copiedBytes < dest.Length)
                {
                    logger.LogInformation("Shortening destination stream from {OldLength} to {NewLength}.", dest.Length, copiedBytes);
                    dest.SetLength(copiedBytes);
                }

                dest.Position = 0;
            }
            finally
            {
                pool.Return(buffer);
            }
        }

        public static async Task SetLengthAndWriteAsync(this Stream stream, long length)
        {
            stream.SetLength(length);
            stream.Position = length - 1;
            await stream.WriteAsync(OneByte);
            stream.Position = 0;
        }

        private static async Task<int> ReadForDurationAsync(Stream src, byte[] buffer, int bufferSize, TimeSpan timeLimit)
        {
            var sw = Stopwatch.StartNew();
            var totalRead = 0;
            int read;
            do
            {
                read = await src.ReadAsync(buffer, totalRead, bufferSize - totalRead);
                totalRead += read;
            }
            while (read > 0 && totalRead < bufferSize && sw.Elapsed < timeLimit);

            return totalRead;
        }
    }
}
