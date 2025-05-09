// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Runtime;
using System.Runtime.InteropServices;

namespace NuGet.Insights
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
        private const string Memory = "memory";

        private readonly TempStreamDirectoryLeaseService _leaseService;
        private readonly int _maxInMemorySize;
        private readonly int _maxInMemoryMB;
        private readonly IReadOnlyList<TempStreamDirectory> _tempDirs;
        private readonly ILogger<TempStreamWriter> _logger;
        private bool _attemptedMemory;
        private bool _skipMemory;
        private int _tempDirIndex;

        public static Func<string> GetTempFileNameFactory(string id, string version, string contextHint, string extension)
        {
            var suffix = $"{id}_{version}";
            if (!string.IsNullOrEmpty(contextHint))
            {
                suffix += "_" + contextHint;
            }

            const string defaultExtension = "tmp";

            if (!string.IsNullOrEmpty(extension))
            {
                var dotIndex = extension.LastIndexOf('.');
                if (dotIndex >= 0)
                {
                    extension = extension.Substring(dotIndex + 1);
                }

                if (!extension.All(char.IsAsciiLetterOrDigit))
                {
                    extension = defaultExtension;
                }
            }
            else
            {
                extension = defaultExtension;
            }

            return () => $"{Guid.NewGuid().ToByteArray().ToTrimmedBase32()}_{suffix}.{extension}";
        }

        public TempStreamWriter(TempStreamDirectoryLeaseService leaseService, IOptions<NuGetInsightsSettings> options, ILogger<TempStreamWriter> logger)
        {
            _leaseService = leaseService;
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

        public async Task<TempStreamResult> CopyToTempStreamAsync(Stream src, Func<string> getTempFileName, long length, IIncrementalHash hashAlgorithm)
        {
            if (length < 0)
            {
                try
                {
                    length = src.Length;
                }
                catch (NotSupportedException)
                {
                    length = -1;
                    _skipMemory = true;
                    _logger.LogInformation("A {TypeName} stream has an unknown length. It will not be buffered to memory.", src.GetType().FullName);
                }
            }

            if (length >= 0)
            {
                _logger.LogInformation("Starting to buffer a {TypeName} stream with length {LengthBytes} bytes.", src.GetType().FullName, length);
            }

            if (length >= 0 && length > _maxInMemorySize)
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
                        return await CopyAndSeekAsync(src, length, hashAlgorithm, dest, Memory, TempStreamDirectory.DefaultBufferSize, NullAsyncDisposable.Instance);
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

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
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

                            if (length >= 0 && (ulong)length > freeBytesAvailable)
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
                    }

                    var tempDirLease = await _leaseService.WaitForLeaseAsync(tempDir);
                    if (tempDirLease is null)
                    {
                        return TempStreamResult.SemaphoreNotAvailable();
                    }

                    var tempPath = Path.Combine(tempDir, getTempFileName());
                    try
                    {
                        _logger.LogInformation("Creating a file stream at location {TempPath}.", tempPath);
                        dest = NewTempFile(tempPath);

                        if (length >= 0 && tempDir.PreallocateFile)
                        {
                            // Pre-allocate the full file size, to encounter full disk exceptions prior to reading the source stream.
                            _logger.LogInformation("Pre-allocating file at location {TempPath} to {Length} bytes.", tempPath, length);
                            await dest.SetLengthAndWriteAsync(length);
                        }

                        consumedSource = true;
                        return await CopyAndSeekAsync(src, length, hashAlgorithm, dest, tempPath, tempDir.BufferSize, tempDirLease);
                    }
                    catch (IOException ex)
                    {
                        SafeDispose(dest);
                        _tempDirIndex++;
                        if (length >= 0)
                        {
                            _logger.LogWarning(ex, "Could not buffer a {TypeName} stream with length {LengthBytes} bytes to temp file {TempFile}.", src.GetType().FullName, length, tempPath);
                        }
                        else
                        {
                            _logger.LogWarning(ex, "Could not buffer a {TypeName} stream with unknown length to temp file {TempFile}.", src.GetType().FullName, tempPath);
                        }
                        await SafeDisposeAsync(tempDirLease);
                    }
                    catch
                    {
                        await SafeDisposeAsync(tempDirLease);
                        throw;
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

        public static FileStream NewTempFile(string tempPath)
        {
            var fileStreamOptions = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose,
            };

            return new FileStream(tempPath, fileStreamOptions);
        }

        private static string DisplayTempDir(TempStreamDirectory tempDir)
        {
            if (!tempDir.MaxConcurrentWriters.HasValue)
            {
                return tempDir.Path;
            }

            return $"{tempDir.Path} (max writers: {tempDir.MaxConcurrentWriters.Value})";
        }

        private async ValueTask SafeDisposeAsync(IAsyncDisposable disposable)
        {
            if (disposable is null)
            {
                return;
            }

            try
            {
                await disposable.DisposeAsync();
            }
            catch
            {
                // Best effort.
            }
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

        private async Task<TempStreamResult> CopyAndSeekAsync(Stream src, long length, IIncrementalHash hashAlgorithm, Stream dest, string location, int bufferSize, IAsyncDisposable lease)
        {
            if (length >= 0)
            {
                _logger.LogInformation(
                    "Starting copy of a {TypeName} stream with length {LengthBytes} bytes to {Location}.",
                    src.GetType().FullName,
                    dest.Length,
                    location);
            }
            else
            {
                _logger.LogInformation(
                    "Starting copy of a {TypeName} stream with unknown length to {Location}.",
                    src.GetType().FullName,
                    location);
            }

            var sw = Stopwatch.StartNew();
            await src.CopyToSlowAsync(dest, length, bufferSize, hashAlgorithm, _logger);
            sw.Stop();

            _logger.LogInformation(
                "Successfully copied a {TypeName} stream with length {LengthBytes} bytes to {Location} in {DurationMs}ms.",
                src.GetType().FullName,
                dest.Length,
                location,
                sw.Elapsed.TotalMilliseconds);

            return TempStreamResult.Success(dest, hashAlgorithm.Output, lease);
        }
    }
}
