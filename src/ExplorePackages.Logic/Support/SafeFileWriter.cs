using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public static class SafeFileWriter
    {
        private const int BufferSize = 8192;

        public static async Task WriteAsync(string destinationPath, Stream sourceStream, ILogger logger)
        {
            await WriteAsync(destinationPath, destStream => sourceStream.CopyToAsync(destStream), logger);
        }

        public static async Task WriteAsync(string destinationPath, Func<Stream, Task> writeAsync, ILogger logger)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

            var newPath = $"{destinationPath}.new";
            var oldPath = $"{destinationPath}.old";

            using (var destStream = new FileStream(
                newPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous))
            {
                await writeAsync(destStream);
            }

            Replace(destinationPath, newPath, oldPath, logger);
        }

        public static void Replace(string destinationPath, string newPath, string oldPath, ILogger logger)
        {
            const int Attempts = 5;
            for (var i = 0; i < Attempts; i++)
            {
                try
                {
                    try
                    {
                        File.Replace(newPath, destinationPath, oldPath);
                        File.Delete(oldPath);
                        return;
                    }
                    catch (FileNotFoundException)
                    {
                        File.Move(newPath, destinationPath);
                        return;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    if (i < Attempts - 1)
                    {
                        var sleepDuration = TimeSpan.FromSeconds(5);
                        logger.LogWarning(ex, "Failed to replace the file. Retrying after {SleepDuration}...", sleepDuration);
                        Thread.Sleep(sleepDuration);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
    }
}
