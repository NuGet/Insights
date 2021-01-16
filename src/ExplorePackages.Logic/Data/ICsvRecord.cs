using System;
using System.IO;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public interface ICsvRecord<T> where T : new()
    {
        void Write(TextWriter writer);
        Task WriteAsync(TextWriter writer);
        T Read(Func<string> getNextField);
    }
}
