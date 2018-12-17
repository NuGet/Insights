using Knapcode.MiniZip;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipContext
    {
        public MZipContext(bool exists, long? size, ZipDirectory zipDirectory)
        {
            Exists = exists;
            Size = size;
            ZipDirectory = zipDirectory;
        }

        public bool Exists { get; }
        public long? Size { get; }
        public ZipDirectory ZipDirectory { get; }
    }
}
