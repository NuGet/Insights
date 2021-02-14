using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages
{
    public class TempStreamWriter
    {
        /// <summary>
        /// We use this method instead of <see cref="DriveInfo.AvailableFreeSpace"/> because it observes quotas at the
        /// specific directory level, instead of the quote at the drive level. It's possible for directories to have
        /// different quotes than the root of the drive. For example, the <c>C:\local</c> directory in Azure Functions
        /// is more limited than <c>C:\home</c> because the former is a local, smaller, faster disk and the latter is a
        /// Azure Storage-based file share which is remote, bigger, and slower. These two have different quotas.
        /// </summary>
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

        private readonly TempStreamLeaseScope _leaseScope;
        private readonly int _maxInMemorySize;
        private readonly int _maxInMemoryMB;
        private readonly IReadOnlyList<TempStreamDirectory> _tempDirs;
        private readonly ILogger<TempStreamWriter> _logger;
        private bool _attemptedMemory;
        private bool _skipMemory;
        private int _tempDirIndex;

        public TempStreamWriter(TempStreamLeaseScope leaseScope, IOptions<ExplorePackagesSettings> options, ILogger<TempStreamWriter> logger)
        {
            _leaseScope = leaseScope;
            _maxInMemorySize = Math.Min(options.Value.MaxTempMemoryStreamSize, GB);
            _maxInMemoryMB = (_maxInMemorySize / MB) + (_maxInMemorySize % MB > 0 ? 1 : 0);
            _tempDirs = options
                .Value
                .TempDirectories
                .Select(x => new TempStreamDirectory
                {
                    Path = Path.GetFullPath(x.Path),
                    MaxConcurrentWriters = x.MaxConcurrentWriters,
                    PreallocateFile = x.PreallocateFile,
                })
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
            return await CopyToTempStreamAsync(src, length, hashAlgorithm: null);
        }

        public async Task<TempStreamResult> CopyToTempStreamAsync(Stream src, long length, HashAlgorithm hashAlgorithm)
        {
            if (length < 0)
            {
                length = src.Length;
            }

            _logger.LogInformation("Starting to buffer a {TypeName} stream with length {LengthBytes} bytes.", src.GetType().FullName, length);

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
                        // 1. Ensure there is at least some fixed margin of memory available. We don't want to put the
                        //    app under too much pressure with these MemoryStream instances.
                        //
                        // 2. Allocation the full memory stream up front to catch try to avoid later OutOfMemoryExceptions.
                        //
                        using (new MemoryFailPoint(_maxInMemoryMB))
                        {
                            dest = new MemoryStream((int)length);
                        }
                    }
                    catch (Exception ex) when (ex is OutOfMemoryException || ex is IOException)
                    {
                        // It's probably a bad idea to catch OutOfMemoryException, but I tried using the MemoryFailPoint
                        // to check for the precise amount of memory (not the max) and UnavailableMemoryException was not
                        // getting thrown as often as I liked. We'll try this and see if it works...
                        //
                        // I've seen IOException get thrown from MemoryFailPoint's constructor when memory is low.
                        dest = null;
                        _logger.LogWarning(ex, "Could not allocate a memory stream of length {LengthBytes} bytes for a {TypeName}.", length, src.GetType().FullName);
                    }

                    if (dest != null)
                    {
                        return await CopyAndSeekAsync(src, length, hashAlgorithm, dest, Memory);
                    }
                }

                // Next, try each temp directory in order.
                while (_tempDirIndex < _tempDirs.Count)
                {
                    if (consumedSource)
                    {
                        return TempStreamResult.NeedNewStream();
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

                    var tempPath = Path.Combine(tempDir, StorageUtility.GenerateDescendingId().ToString());
                    if (!await _leaseScope.WaitAsync(tempDir))
                    {
                        return TempStreamResult.SemaphoreNotAvailable();
                    }

                    try
                    {
                        _logger.LogInformation("Creating a file stream at location {TempPath}.", tempPath);
                        dest = new FileStream(
                            tempPath,
                            FileMode.Create,
                            FileAccess.ReadWrite,
                            FileShare.None,
                            BufferSize,
                            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

                        if (tempDir.PreallocateFile)
                        {
                            // Pre-allocate the full file size, to encounter full disk exceptions prior to reading the source stream.
                            _logger.LogInformation("Pre-allocating file at location {TempPath} to {Length} bytes.", tempPath, length);
                            await SetStreamLength((FileStream)dest, length);
                        }

                        consumedSource = true;
                        return await CopyAndSeekAsync(src, length, hashAlgorithm, dest, tempPath);
                    }
                    catch (IOException ex)
                    {
                        SafeDispose(dest);
                        _tempDirIndex++;
                        _logger.LogWarning(ex, "Could not buffer a {TypeName} stream with length {LengthBytes} bytes to temp file {TempFile}.", src.GetType().FullName, length, tempPath);
                    }
                }

                throw new InvalidOperationException(
                    "Unable to find a place to copy the stream. Tried:" + Environment.NewLine +
                    string.Join(Environment.NewLine, Enumerable.Empty<string>()
                        .Concat(_attemptedMemory ? new[] { Memory } : Array.Empty<string>())
                        .Concat(_tempDirs.Select(DisplayTempDir))
                        .Select(x => $" - {x}")));
            }
            catch
            {
                SafeDispose(dest);
                throw;
            }
        }

        private static string DisplayTempDir(TempStreamDirectory tempDir)
        {
            if (!tempDir.MaxConcurrentWriters.HasValue)
            {
                return tempDir.Path;
            }

            return $"{tempDir.Path} (max writers: {tempDir.MaxConcurrentWriters.Value})";
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

        private async Task<TempStreamResult> CopyAndSeekAsync(Stream src, long length, HashAlgorithm hashAlgorithm, Stream dest, string location)
        {
            _logger.LogInformation(
                "Starting copy of a {TypeName} stream with length {LengthBytes} bytes to {Location}.",
                src.GetType().FullName,
                dest.Length,
                location);

            var sw = Stopwatch.StartNew();
            await CopyAndSeekAsync(src, length, hashAlgorithm, dest);
            sw.Stop();

            _logger.LogInformation(
                "Successfully copied a {TypeName} stream with length {LengthBytes} bytes to {Location} in {DurationMs}ms.",
                src.GetType().FullName,
                dest.Length,
                location,
                sw.Elapsed.TotalMilliseconds);

            return TempStreamResult.Success(dest, hashAlgorithm?.Hash);
        }

        private async Task CopyAndSeekAsync(Stream src, long length, HashAlgorithm hashAlgorithm, Stream dest)
        {
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(BufferSize);
            try
            {
                long copiedBytes = 0;
                double previousPercent = -1;
                while (true)
                {
                    // Spend up to 5 second trying to fill up the buffer. This results is less chattiness on the "write" side
                    // of the copy operation.
                    var bytesRead = await ReadForDurationAsync(src, buffer, TimeSpan.FromSeconds(5));

                    copiedBytes += bytesRead;
                    var percent = 1.0 * copiedBytes / length;
                    var logLevel = bytesRead == 0 || (int)(percent * 100) != (int)(previousPercent * 100) ? LogLevel.Information : LogLevel.Debug;
                    _logger.Log(logLevel, "Read {BufferBytes} bytes ({CopiedBytes} of {TotalBytes}, {Percent:P2}).", bytesRead, copiedBytes, length, percent);

                    if (bytesRead == 0)
                    {
                        hashAlgorithm?.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        break;
                    }

                    hashAlgorithm?.TransformBlock(buffer, 0, bytesRead, buffer, 0);

                    await dest.WriteAsync(buffer, 0, bytesRead);
                    _logger.Log(logLevel, "Wrote {BufferBytes} bytes ({CopiedBytes} of {TotalBytes}, {Percent:P2}).", bytesRead, copiedBytes, length, percent);
                    previousPercent = percent;
                }

                if (copiedBytes < dest.Length)
                {
                    _logger.LogInformation("Shortening destination stream from {OldLength} to {NewLength}.", dest.Length, copiedBytes);
                    dest.SetLength(copiedBytes);
                }

                dest.Position = 0;
            }
            finally
            {
                pool.Return(buffer);
            }
        }

        private static async Task<int> ReadForDurationAsync(Stream src, byte[] buffer, TimeSpan timeLimit)
        {
            var sw = Stopwatch.StartNew();
            var totalRead = 0;
            int read;
            do
            {
                read = await src.ReadAsync(buffer, totalRead, buffer.Length - totalRead);
                totalRead += read;
            }
            while (read > 0 && totalRead < buffer.Length && sw.Elapsed < timeLimit);

            return totalRead;
        }
    }
}
