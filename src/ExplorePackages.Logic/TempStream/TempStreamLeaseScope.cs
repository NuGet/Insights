using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages
{
    public class TempStreamLeaseScope
    {
        private readonly StorageSemaphoreLeaseService _semaphoreService;
        private readonly ILogger<TempStreamLeaseScope> _logger;
        private bool _disposing;
        private int _owned;
        private int _inProgressCount;
        private int _acquiredCount;
        private readonly ConcurrentDictionary<string, Lazy<Task<AutoRenewingStorageLeaseResult>>> _nameToLazyLease
            = new ConcurrentDictionary<string, Lazy<Task<AutoRenewingStorageLeaseResult>>>();

        public TempStreamLeaseScope(StorageSemaphoreLeaseService semaphoreService, ILogger<TempStreamLeaseScope> logger)
        {
            _semaphoreService = semaphoreService;
            _logger = logger;
            _disposing = false;
            _owned = 0;
            _acquiredCount = 0;
        }

        public IAsyncDisposable TakeOwnership()
        {
            if (Interlocked.CompareExchange(ref _owned, 1, 0) == 1)
            {
                throw new InvalidOperationException("The scope is already owned.");
            }

            return new Ownership(this);
        }

        public async Task<bool> WaitAsync(TempStreamDirectory dir)
        {
            if (_disposing)
            {
                throw new ObjectDisposedException(nameof(TempStreamLeaseScope));
            }

            if (_owned == 0)
            {
                throw new InvalidOperationException("The scope is not owned yet and therefore can't acquire leases.");
            }

            if (!dir.MaxConcurrentWriters.HasValue)
            {
                return true;
            }

            string dirHash;
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(dir.Path.ToLowerInvariant());
                dirHash = sha256.ComputeHash(bytes).ToTrimmedBase32();
            }

            var name = $"temp-dir-{dirHash}";

            var lazy = _nameToLazyLease.GetOrAdd(name, x => GetLazyLease(x, dir.MaxConcurrentWriters.Value, dir.SemaphoreTimeout));
            if (lazy.IsValueCreated && lazy.Value.Status == TaskStatus.RanToCompletion)
            {
                if (!(await lazy.Value).Acquired)
                {
                    return false;
                }
                else
                {
                    _logger.LogInformation("The semaphore {Name} for {TempDir} has already been acquired in this scope.", name, dir);
                }
            }
            else
            {
                _logger.LogInformation("Starting to wait for semaphore {Name} for {TempDir}.", name, dir);
            }

            var lease = await lazy.Value;
            return lease.Acquired;
        }

        private Lazy<Task<AutoRenewingStorageLeaseResult>> GetLazyLease(string name, int count, TimeSpan timeout)
        {
            return new Lazy<Task<AutoRenewingStorageLeaseResult>>(async () =>
            {
                Interlocked.Increment(ref _inProgressCount);
                var result = await _semaphoreService.WaitAsync(name, count, timeout);
                if (result.Acquired)
                {
                    Interlocked.Increment(ref _acquiredCount);
                }
                Interlocked.Decrement(ref _inProgressCount);

                return result;
            });
        }

        private class Ownership : IAsyncDisposable
        {
            private readonly TempStreamLeaseScope _scope;

            public Ownership(TempStreamLeaseScope scope)
            {
                _scope = scope;
            }

            public async ValueTask DisposeAsync()
            {
                _scope._disposing = true;
                if (_scope._nameToLazyLease.Any() && (_scope._inProgressCount > 0 || _scope._acquiredCount > 0))
                {
                    _scope._logger.LogInformation("Waiting for scope semaphores: {Names}", _scope._nameToLazyLease.Keys);
                    var leases = await Task.WhenAll(_scope._nameToLazyLease.Values.Select(x => x.Value));
                    _scope._logger.LogInformation("Starting to release scope semaphores: {Names}", _scope._nameToLazyLease.Keys);
                    await Task.WhenAll(leases.Select(x => x.DisposeAsync().AsTask()));
                    _scope._logger.LogInformation("Done release scope semaphores: {Names}", _scope._nameToLazyLease.Keys);
                }
            }
        }
    }
}
