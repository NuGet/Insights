using System;
using System.IO;

namespace Knapcode.ExplorePackages
{
    public interface ICsvRecord<T>
    {
        public void Write(TextWriter writer);
        public T Read(Func<string> getNextField);
    }
}
