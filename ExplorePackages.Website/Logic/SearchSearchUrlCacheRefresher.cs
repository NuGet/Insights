using System;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Microsoft.Extensions.Hosting;

namespace Knapcode.ExplorePackages.Website.Logic
{
    /// <summary>
    /// Based off of: <see cref="https://gist.github.com/davidfowl/a7dd5064d9dcf35b6eae1a7953d615e3"/>.
    /// </summary>
    public class SearchSearchUrlCacheRefresher : IHostedService
    {
        private CancellationTokenSource _cts;
        private Task _executingTask;
        private readonly SearchServiceUrlDiscoverer _urlDiscoverer;

        public SearchSearchUrlCacheRefresher(SearchServiceUrlDiscoverer urlDiscoverer)
        {
            _urlDiscoverer = urlDiscoverer;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            _executingTask = ExecuteAsync(_cts.Token);
            
            if (_executingTask.IsCompleted)
            {
                return _executingTask;
            }
            
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_executingTask == null)
            {
                return;
            }
            
            _cts.Cancel();
            
            await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken));
            
            cancellationToken.ThrowIfCancellationRequested();
        }

        private async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _urlDiscoverer.GetUrlsAsync(
                    ServiceIndexTypes.V2Search,
                    specificInstances: true);

                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }
    }
}
