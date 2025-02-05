// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Buffers;

namespace NuGet.Insights
{
    public static class SlowStreamExtensions
    {
        private static readonly ReadOnlyMemory<byte> OneByte = new([0]);

        public static void CopyToSlow(
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
                    var bytesRead = ReadForDuration(src, buffer, bufferSize, TimeSpan.FromSeconds(5));

                    copiedBytes += bytesRead;

                    double percent = default;
                    LogLevel logLevel = default;
                    if (length >= 0)
                    {
                        percent = 1.0 * copiedBytes / length;
                        logLevel = (bytesRead == 0 && previousPercent != 1.0) || (int)(percent * 100) != (int)(previousPercent * 100) ? LogLevel.Information : LogLevel.None;
                        if (logLevel != LogLevel.None)
                        {
                            logger.Log(logLevel, "Read {BufferBytes} bytes ({CopiedBytes} of {TotalBytes}, {Percent:P2}).", bytesRead, copiedBytes, length, percent);
                        }
                    }

                    if (bytesRead == 0)
                    {
                        hashAlgorithm.TransformFinalBlock();
                        break;
                    }

                    hashAlgorithm.TransformBlock(buffer, 0, bytesRead);

                    dest.Write(buffer, 0, bytesRead);

                    if (length >= 0)
                    {
                        if (logLevel != LogLevel.None)
                        {
                            logger.Log(logLevel, "Wrote {BufferBytes} bytes ({CopiedBytes} of {TotalBytes}, {Percent:P2}).", bytesRead, copiedBytes, length, percent);
                        }
                        previousPercent = percent;
                    }
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

        public static async Task CopyToSlowAsync(
            this Stream src,
            Stream dest,
            long length,
            int bufferSize,
            IIncrementalHash hasher,
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

                    double percent = default;
                    LogLevel logLevel = default;
                    if (length >= 0)
                    {
                        percent = 1.0 * copiedBytes / length;
                        logLevel = (bytesRead == 0 && previousPercent != 1.0) || (int)(percent * 100) != (int)(previousPercent * 100) ? LogLevel.Information : LogLevel.None;
                        if (logLevel != LogLevel.None)
                        {
                            logger.Log(logLevel, "Read {BufferBytes} bytes ({CopiedBytes} of {TotalBytes}, {Percent:P2}).", bytesRead, copiedBytes, length, percent);
                        }
                    }

                    if (bytesRead == 0)
                    {
                        hasher.TransformFinalBlock();
                        break;
                    }

                    hasher.TransformBlock(buffer, 0, bytesRead);

                    await dest.WriteAsync(buffer, 0, bytesRead);

                    if (length >= 0)
                    {
                        if (logLevel != LogLevel.None)
                        {
                            logger.Log(logLevel, "Wrote {BufferBytes} bytes ({CopiedBytes} of {TotalBytes}, {Percent:P2}).", bytesRead, copiedBytes, length, percent);
                        }
                        previousPercent = percent;
                    }
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

        public static void SetLengthAndWrite(this Stream stream, long length)
        {
            stream.SetLength(length);

            if (length > 0)
            {
                stream.Position = length - 1;
                stream.Write(OneByte.Span);
            }

            stream.Position = 0;
        }

        public static async Task SetLengthAndWriteAsync(this Stream stream, long length)
        {
            stream.SetLength(length);
            stream.Position = length - 1;
            await stream.WriteAsync(OneByte);
            stream.Position = 0;
        }

        private static int ReadForDuration(Stream src, byte[] buffer, int bufferSize, TimeSpan timeLimit)
        {
            var sw = Stopwatch.StartNew();
            var totalRead = 0;
            int read;
            do
            {
                read = src.Read(buffer, totalRead, bufferSize - totalRead);
                totalRead += read;
            }
            while (read > 0 && totalRead < bufferSize && sw.Elapsed < timeLimit);

            return totalRead;
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
