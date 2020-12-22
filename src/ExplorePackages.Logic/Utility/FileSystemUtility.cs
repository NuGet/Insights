using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public class BufferService
    {
        private const int MB = 1024 * 1024;

        private readonly IOptions<ExplorePackagesSettings> _options;

        public BufferService(IOptions<ExplorePackagesSettings> options)
        {
            _options = options;
        }

        public async Task<Stream> CopyToTempStreamAsync(Stream src)
        {
            var lengthMB = (int)((src.Length / MB) + (src.Length % MB != 0 ? 1 : 0));
            MemoryStream memoryStream = null;
            using (var memoryFailPoint = new MemoryFailPoint(lengthMB))
            {

            }
            
        }
    }

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
