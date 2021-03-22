using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;

namespace Knapcode.ExplorePackages
{
    public static class AsyncPageableExtensions
    {
        public static async Task<List<T>> ToListAsync<T>(this AsyncPageable<T> asyncPageable, QueryLoopMetrics metrics)
        {
            using (metrics)
            {
                var enumerator = asyncPageable.AsPages().GetAsyncEnumerator();
                try
                {
                    var output = new List<T>();
                    while (await MoveNextAsync(enumerator, metrics))
                    {
                        output.AddRange(enumerator.Current.Values);
                    }
                    return output;
                }
                finally
                {
                    await enumerator.DisposeAsync();
                }
            }

        }

        private static async Task<bool> MoveNextAsync<T>(IAsyncEnumerator<Page<T>> enumerator, QueryLoopMetrics metrics)
        {
            using (metrics.TrackQuery())
            {
                return await enumerator.MoveNextAsync();
            }
        }
    }
}
