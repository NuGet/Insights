using System;
using System.IO;

namespace Knapcode.ExplorePackages
{
    public interface ICsvRecord<T> where T : new()
    {
        public void Write(TextWriter writer);
        public T Read(Func<string> getNextField);
    }
}
