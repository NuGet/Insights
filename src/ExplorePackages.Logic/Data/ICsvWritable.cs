using System.IO;

namespace Knapcode.ExplorePackages
{
    public interface ICsvWritable
    {
        public void Write(TextWriter writer);
    }
}
