using System.IO;

namespace Knapcode.ExplorePackages
{
    public static class PathUtility
    {
        private static readonly char[] DirectorySeparators = new[] { Path.DirectorySeparatorChar, '/', '\\' };

        public static string GetTopLevelFolder(string relativePath)
        {
            var firstSlashIndex = relativePath.IndexOfAny(DirectorySeparators);
            if (firstSlashIndex < 0)
            {
                return null;
            }

            return relativePath.Substring(0, firstSlashIndex);
        }
    }
}
