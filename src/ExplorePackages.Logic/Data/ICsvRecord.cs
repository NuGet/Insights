using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public interface ICsvRecord<T> where T : new()
    {
        int FieldCount { get; }
        void WriteHeader(TextWriter writer);
        void Write(List<string> fields);
        void Write(TextWriter writer);
        Task WriteAsync(TextWriter writer);
        T Read(Func<string> getNextField);
    }
}
