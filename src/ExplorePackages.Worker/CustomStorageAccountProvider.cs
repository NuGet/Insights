using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;

namespace Knapcode.ExplorePackages.Worker
{
    public class CustomStorageAccountProvider : StorageAccountProvider, IDisposable
    {
        public const string ConnectionName = nameof(CustomStorageAccountProvider) + ":StorageAccount";

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ILogger<CustomStorageAccountProvider> _logger;

        private readonly object _storageAccountLock = new object();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private StorageAccount _storageAccount;
        private Task _refreshSasTask;

        public CustomStorageAccountProvider(
            IConfiguration configuration,
            ServiceClientFactory serviceClientFactory,
            ILogger<CustomStorageAccountProvider> logger) : base(configuration)
        {
            _serviceClientFactory = serviceClientFactory;
            _logger = logger;
        }
        public override StorageAccount Get(string name)
        {
            switch (name)
            {
                case ConnectionName:
                    return GetStorageAccount();
                default:
                    return base.Get(name);
            }
        }

        private StorageAccount GetStorageAccount()
        {
            lock (_storageAccountLock)
            {
                if (_storageAccount != null)
                {
                    return _storageAccount;
                }

                (var connectionString, var untilRefresh) = _serviceClientFactory.GetStorageConnectionSync();
                _storageAccount = StorageAccount.NewFromConnectionString(connectionString);

                _logger.LogInformation("Starting the storage credential refresh task.");
                _refreshSasTask = RefreshAsync(untilRefresh);

                return _storageAccount;
            }
        }

        public void Dispose()
        {
            if (_refreshSasTask != null)
            {
                _cts.Cancel();

                try
                {
                    var completed = _refreshSasTask.Wait(TimeSpan.FromSeconds(30));
                    if (!completed)
                    {
                        _logger.LogError("The storage credential refresh task did not complete gracefully.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Stopping the credential refresh task failed.");
                }

                _refreshSasTask = null;
            }

            _cts.Dispose();
        }


        private async Task RefreshAsync(TimeSpan untilRefresh)
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("The storage credential refresh task is waiting for {DurationMinutes:F2} minutes.", untilRefresh.TotalMinutes);
                    await Task.Delay(untilRefresh, _cts.Token);

                    _logger.LogInformation("The storage credential refresh task is fetching a new credentials.");
                    (var connectionString, var nextUntilRefresh) = await _serviceClientFactory.GetStorageConnectionAsync(_cts.Token);

                    // Update the SAS token or key, depending on what is currently used.
                    var newCredentials = CloudStorageAccount.Parse(connectionString).Credentials;
                    var existingCredentials = _storageAccount.SdkObject.Credentials;
                    if (existingCredentials.IsSAS)
                    {
                        existingCredentials.UpdateSASToken(newCredentials.SASToken);
                    }
                    else
                    {
                        existingCredentials.UpdateKey(newCredentials.ExportBase64EncodedKey());
                    }

                    untilRefresh = nextUntilRefresh;
                }
                catch (OperationCanceledException)
                {
                    // Expected on cancel.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The storage credential refresh task failed to new credentials.");

                    // Best effort, try again soon.
                    untilRefresh = TimeSpan.FromMinutes(1);
                }
            }
        }
    }
}
