using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public interface ICsvRecord
    {
        int FieldCount { get; }
        void WriteHeader(TextWriter writer);
        void Write(List<string> fields);
        void Write(TextWriter writer);
        Task WriteAsync(TextWriter writer);
        ICsvRecord ReadNew(Func<string> getNextField);
    }
}
