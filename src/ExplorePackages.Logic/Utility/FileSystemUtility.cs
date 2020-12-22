using System;
using System.IO;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public static class FileSystemUtility
    {
        public static async Task<FileStream> CopyToTempStreamAsync(string baseDir, Stream src)
        {
            FileStream dest = GetTempStream(baseDir);
            try
            {
                await src.CopyToAsync(dest);
                dest.Position = 0;
                return dest;
            }
            catch
            {
                dest?.Dispose();
                throw;
            }
        }

        public static FileStream GetTempStream(string baseDir)
        {
            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            return new FileStream(
                Path.Combine(baseDir, Guid.NewGuid().ToString("N")),
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 80 * 1024,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        }
    }
}
