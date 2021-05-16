using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;

namespace NuGet.Insights
{
    public static class AsyncPageableExtensions
    {
        public static async Task<List<T>> ToListAsync<T>(this AsyncPageable<T> asyncPageable, QueryLoopMetrics metrics)
        {
            using (metrics)
            {
                await using var enumerator = asyncPageable.AsPages().GetAsyncEnumerator();
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

        public static async Task<bool> MoveNextAsync<T>(this IAsyncEnumerator<Page<T>> enumerator, QueryLoopMetrics metrics)
        {
            using (metrics.TrackQuery())
            {
                return await enumerator.MoveNextAsync();
            }
        }
    }
}
