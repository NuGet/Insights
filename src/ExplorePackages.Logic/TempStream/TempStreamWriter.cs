using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages
{
    public class TempStreamWriter
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetDiskFreeSpaceEx(
            string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        private const int MB = 1024 * 1024;
        private const int GB = 1024 * MB;
        private const int BufferSize = 80 * 1024;
        private const string Memory = "memory";
        private static readonly ReadOnlyMemory<byte> OneByte = new ReadOnlyMemory<byte>(new[] { (byte)0 });

        private readonly int _maxInMemorySize;
        private readonly IReadOnlyList<string> _tempDirs;
        private readonly ILogger _logger;
        private bool _attemptedMemory;
        private bool _skipMemory;
        private int _tempDirIndex;

        public TempStreamWriter(IOptions<ExplorePackagesSettings> options, ILogger logger)
        {
            _maxInMemorySize = Math.Min(options.Value.MaxTempMemoryStreamSize, GB);
            _tempDirs = options
                .Value
                .TempDirectories
                .Select(x => Path.GetFullPath(x.Path))
                .ToList();
            _logger = logger;
            _attemptedMemory = false;
            _skipMemory = false;
            _tempDirIndex = 0;
        }

        public async Task<TempStreamResult> CopyToTempStreamAsync(Stream src)
        {
            return await CopyToTempStreamAsync(src, -1);
        }

        public async Task<TempStreamResult> CopyToTempStreamAsync(Stream src, long length)
        {
            if (length < 0)
            {
                length = src.Length;
            }

            _logger.LogInformation("Starting to buffer a {TypeName} stream with length {LengthBytes} bytes.", src.GetType().FullName, length);

            if (length == 0)
            {
                _logger.LogInformation("Successfully copied an empty {TypeName} stream.", src.GetType().FullName);
                return TempStreamResult.NewSuccess(Stream.Null);
            }

            if (length > _maxInMemorySize)
            {
                _skipMemory = true;
                _logger.LogInformation("A {TypeName} stream is greater than {MaxInMemorySize} bytes. It will not be buffered to memory.", src.GetType().FullName, _maxInMemorySize);
            }

            Stream dest = null;
            var consumedSource = false;
            try
            {
                // First, try to buffer to memory.
                if (!_skipMemory && !_attemptedMemory)
                {
                    _attemptedMemory = true;

                    try
                    {
                        dest = new MemoryStream((int)length);
                    }
                    catch (OutOfMemoryException ex)
                    {
                        // It's probably a bad idea to catch OutOfMemoryException. I tried using a MemoryFailPoint but
                        // it was not producing InsufficientMemoryException in all of the cases I expected so let's try
                        // this instead and see what blows up.
                        dest = null;
                        _logger.LogWarning(ex, "Could not allocate a memory stream of length {LengthBytes} bytes for a {TypeName}.", length, src.GetType().FullName);
                    }

                    if (dest != null)
                    {
                        return await CopyAndSeekAsync(src, dest, Memory);
                    }
                }

                // Next, try each temp directory in order.
                while (_tempDirIndex < _tempDirs.Count)
                {
                    if (consumedSource)
                    {
                        return TempStreamResult.NewFailure();
                    }

                    var tempDir = _tempDirs[_tempDirIndex];
                    if (!Directory.Exists(tempDir))
                    {
                        Directory.CreateDirectory(tempDir);
                    }

                    // Try to check if there is enough space on the drive.
                    if (!GetDiskFreeSpaceEx(tempDir, out var freeBytesAvailable, out var totalNumberOfBytes, out var totalNumberOfFreeBytes))
                    {
                        try
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }
                        catch (Win32Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not determine available free space in temp dir {TempDir}.", tempDir);
                        }
                    }
                    else
                    {
                        _logger.LogInformation(
                            "For temp dir {TempDir}, there are {FreeBytesAvailable} bytes available (total: {TotalNumberOfBytes}, total free: {TotalNumberOfFreeBytes}).",
                            tempDir,
                            freeBytesAvailable,
                            totalNumberOfBytes,
                            totalNumberOfFreeBytes);

                        if ((ulong)length > freeBytesAvailable)
                        {
                            _tempDirIndex++;
                            _logger.LogWarning(
                                "Not enough space in temp dir {TempDir} to buffer a {TypeName} stream with length {LengthBytes} bytes (only {FreeBytesAvailable} bytes available).",
                                tempDir,
                                src.GetType().FullName,
                                length,
                                freeBytesAvailable);
                            continue;
                        }
                    }

                    var tmpPath = Path.Combine(tempDir, StorageUtility.GenerateDescendingId().ToString());
                    FileStream destFileStream = null;
                    try
                    {
                        dest = new FileStream(
                            tmpPath,
                            FileMode.Create,
                            FileAccess.ReadWrite,
                            FileShare.None,
                            BufferSize,
                            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

                        destFileStream = (FileStream)dest;

                        // Pre-allocate the full file size, to encounter full disk exceptions prior to reading the source stream.
                        await SetStreamLength(destFileStream, length);

                        consumedSource = true;
                        return await CopyAndSeekAsync(src, dest, tmpPath);
                    }
                    catch (IOException ex)
                    {
                        SafeDispose(dest);
                        _tempDirIndex++;
                        _logger.LogWarning(ex, "Could not buffer a {TypeName} stream with length {LengthBytes} bytes to temp file {TempFile}.", src.GetType().FullName, length, tmpPath);
                    }
                }

                throw new InvalidOperationException(
                    "Unable to find a place to copy the stream. Tried:" + Environment.NewLine +
                    string.Join(Environment.NewLine, Enumerable.Empty<string>()
                        .Concat(_attemptedMemory ? new[] { Memory } : Array.Empty<string>())
                        .Concat(_tempDirs)
                        .Select(x => $" - {x}")));
            }
            catch
            {
                SafeDispose(dest);
                throw;
            }
        }

        private static async Task SetStreamLength(Stream stream, long length)
        {
            stream.SetLength(length);
            stream.Position = length - 1;
            await stream.WriteAsync(OneByte);
            stream.Position = 0;
        }

        private static void SafeDispose(Stream dest)
        {
            var isFileStream = false;
            if (dest is FileStream fileStream)
            {
                isFileStream = true;

                try
                {
                    fileStream.SetLength(0);
                }
                catch
                {
                    // Best effort.
                }

                try
                {
                    fileStream.SafeFileHandle?.Close();
                }
                catch
                {
                    // Best effort.
                }
            }

            try
            {
                dest?.Dispose();
            }
            catch (IOException) when (isFileStream)
            {
                // Dispose of a FileStream can fail with an IOException because it can flush some remaining bytes to
                // disk, which in turn causes an "out of disk space" IOException. We ignore this exception in order to
                // try another disk location.
            }
        }

        private async Task<TempStreamResult> CopyAndSeekAsync(Stream src, Stream dest, string location)
        {
            var sw = Stopwatch.StartNew();
            var copiedBytes = await CopyToAndCountAsync(src, dest);
            if (copiedBytes < dest.Length)
            {
                dest.SetLength(copiedBytes);
            }
            sw.Stop();
            dest.Position = 0;
            _logger.LogInformation(
                "Successfully copied a {TypeName} stream with length {LengthBytes} bytes to {Location} in {DurationMs} ms.",
                src.GetType().FullName,
                dest.Length,
                location,
                sw.Elapsed.TotalMilliseconds);
            return TempStreamResult.NewSuccess(dest);
        }

        private static async Task<long> CopyToAndCountAsync(Stream src, Stream dest)
        {
            var buffer = new byte[BufferSize];
            long count = 0;
            while (true)
            {
                int num;
                int bytesRead = (num = await src.ReadAsync(buffer, 0, buffer.Length));
                if (num == 0)
                {
                    break;
                }
                await dest.WriteAsync(buffer, 0, bytesRead);
                count += bytesRead;
            }

            return count;
        }
    }
}
