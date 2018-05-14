using System;
using System.IO;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public static class SafeFileWriter
    {
        private const int BufferSize = 8192;

        public static async Task WriteAsync(string destinationPath, Stream sourceStream)
        {
            await WriteAsync(destinationPath, destStream => sourceStream.CopyToAsync(destStream));
        }

        public static async Task WriteAsync(string destinationPath, Func<Stream, Task> writeAsync)
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

            Replace(destinationPath, newPath, oldPath);
        }

        public static void Replace(string destinationPath, string newPath, string oldPath)
        {
            try
            {
                File.Replace(newPath, destinationPath, oldPath);
                File.Delete(oldPath);
            }
            catch (FileNotFoundException)
            {
                File.Move(newPath, destinationPath);
            }
        }
    }
}
