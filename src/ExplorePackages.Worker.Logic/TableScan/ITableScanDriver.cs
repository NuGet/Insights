using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ITableScanDriver<T>
    {
        IList<string> SelectColumns { get; }
        Task InitializeAsync(JToken parameters);
        Task ProcessEntitySegmentAsync(string tableName, JToken parameters, IReadOnlyList<T> entities);
    }
}
