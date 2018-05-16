using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public static class FileUtility
    {
        public static void DeleteEmptyDirectories(string path)
        {
            string[] directories;
            try
            {
                 directories = Directory.GetDirectories(path);
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }

            foreach (var directory in directories)
            {
                DeleteEmptyDirectories(directory);
                DeleteDirectoryIfEmpty(directory);
            }
        }

        public static void DeleteDirectoryIfEmpty(string directory)
        {
            IEnumerable<string> fileSystemEntries;
            try
            {
                fileSystemEntries = Directory.EnumerateFileSystemEntries(directory);
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }

            if (!fileSystemEntries.Any())
            {
                Directory.Delete(directory, false);
            }
        }
    }
}
