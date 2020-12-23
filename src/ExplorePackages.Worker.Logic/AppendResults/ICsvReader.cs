using System.Collections.Generic;
using System.IO;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICsvReader
    {
        List<T> GetRecords<T>(TextReader reader) where T : ICsvRecord, new();
    }
}
