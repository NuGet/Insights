using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public class TempStreamLeaseScope
    {
        private readonly StorageSemaphoreLeaseService _semaphoreService;
        private bool _disposing;
        private int _owned;
        private readonly ConcurrentDictionary<string, Lazy<Task<AutoRenewingStorageLeaseResult>>> _nameToLazyLease
            = new ConcurrentDictionary<string, Lazy<Task<AutoRenewingStorageLeaseResult>>>();

        public TempStreamLeaseScope(StorageSemaphoreLeaseService semaphoreService)
        {
            _semaphoreService = semaphoreService;
            _disposing = false;
        }

        public IAsyncDisposable TakeOwnership()
        {
            if (Interlocked.CompareExchange(ref _owned, 1, 0) == 1)
            {
                throw new InvalidOperationException("The scope is already owned.");
            }

            return new Ownership(this);
        }

        public async Task WaitAsync(TempStreamDirectory dir)
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
                return;
            }

            string dirHash;
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(dir.Path.ToLowerInvariant());
                dirHash = sha256.ComputeHash(bytes).ToTrimmedBase32();
            }

            var name = $"temp-dir-{dirHash}";

            await _nameToLazyLease.GetOrAdd(name, x => GetLazyLease(x, dir.MaxConcurrentWriters.Value)).Value;
        }

        private Lazy<Task<AutoRenewingStorageLeaseResult>> GetLazyLease(string name, int count)
        {
            return new Lazy<Task<AutoRenewingStorageLeaseResult>>(() => _semaphoreService.WaitAsync(name, count));
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
                var leases = await Task.WhenAll(_scope._nameToLazyLease
                    .Values
                    .Where(x => x.IsValueCreated)
                    .Select(x => x.Value));
                await Task.WhenAll(leases.Select(x => x.DisposeAsync().AsTask()));
            }
        }
    }
}
