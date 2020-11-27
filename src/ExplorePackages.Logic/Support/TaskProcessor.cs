using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public static class TaskProcessor
    {
        public static async Task<ConcurrentBag<TOutput>> ExecuteAsync<TInput, TOutput>(
            IEnumerable<TInput> input,
            Func<TInput, Task<TOutput>> executeAsync,
            int workerCount,
            CancellationToken token)
        {
            var work = new ConcurrentBag<TInput>(input);
            var output = new ConcurrentBag<TOutput>();
            var tasks = Enumerable
                .Range(0, workerCount)
                .Select(async _ =>
                {
                    await Task.Yield();
                    while (work.TryTake(out var item))
                    {
                        token.ThrowIfCancellationRequested();

                        var result = await executeAsync(item);
                        output.Add(result);
                    }
                })
                .ToList();
            await Task.WhenAll(tasks);
            return output;
        }
    }
}
