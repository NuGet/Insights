using System.IO;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public static class FileSystemUtility
    {
        public static async Task<FileStream> CopyToTempStreamAsync(Stream src)
        {
            FileStream dest = GetTempStream();
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

        public static FileStream GetTempStream()
        {
            return new FileStream(
                Path.GetTempFileName(),
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 80 * 1024,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        }
    }
}
