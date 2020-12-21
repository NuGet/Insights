using System.Collections.Generic;
using System.IO;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICsvReader
    {
        List<T> GetRecords<T>(MemoryStream stream) where T : ICsvRecord, new();
    }
}
