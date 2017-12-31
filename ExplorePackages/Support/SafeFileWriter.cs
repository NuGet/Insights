using System;
using System.IO;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Support
{
    public static class SafeFileWriter
    {
        private const int BufferSize = 8192;

        public static async Task WriteAsync(string path, Stream sourceStream)
        {
            await WriteAsync(path, destStream => sourceStream.CopyToAsync(destStream));
        }

        public static async Task WriteAsync(string path, Func<Stream, Task> writeAsync)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var newPath = $"{path}.new";
            var oldPath = $"{path}.old";

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

            try
            {
                File.Replace(newPath, path, oldPath);
                File.Delete(oldPath);
            }
            catch (FileNotFoundException)
            {
                File.Move(newPath, path);
            }
        }
    }
}
