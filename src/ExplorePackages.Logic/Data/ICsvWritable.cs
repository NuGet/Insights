using System;
using System.IO;

namespace Knapcode.ExplorePackages
{
    public interface ICsvWritable
    {
        public void Write(TextWriter writer);
        public void Read(Func<int, string> getField);
    }
}
