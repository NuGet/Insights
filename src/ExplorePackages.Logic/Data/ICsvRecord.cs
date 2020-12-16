using System;
using System.IO;

namespace Knapcode.ExplorePackages
{
    public interface ICsvRecord
    {
        public void Write(TextWriter writer);
        public void Read(Func<string> getNextField);
    }
}
