using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class UpdateSecretsTimer : ITimer
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly HttpClient _httpClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<UpdateSecretsTimer> _logger;

        public UpdateSecretsTimer(
            ServiceClientFactory serviceClientFactory,
            HttpClient httpClient,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<UpdateSecretsTimer> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _httpClient = httpClient;
            _options = options;
            _logger = logger;
        }

        public string Name => "UpdateSecrets";
        public TimeSpan Frequency => TimeSpan.FromHours(6);
        public bool AutoStart => true;
        public bool IsEnabled
        {
            get
            {
                return _options.Value.KeyVaultName != null
                    && _options.Value.StorageSharedAccessSignatureSecretName != null
                    && _options.Value.StorageConnectionStringSecretName != null
                    && _options.Value.HostSubscriptionId != null
                    && _options.Value.HostResourceGroupName != null
                    && _options.Value.HostAppName != null;
            }
        }

        public async Task ExecuteAsync()
        {
            // Update the storage connection with the latest SAS token.
            await UpdateStorageConnectionStringAsync();

            // Restart the function apps to pick up the latest connection string.
            await RestartFunctionApps();
        }

        private async Task UpdateStorageConnectionStringAsync()
        {
            _logger.LogInformation("Getting the latest SAS token from Key Vault.");
            var secretClient = await _serviceClientFactory.GetKeyVaultSecretClientAsync();
            KeyVaultSecret sas = await secretClient.GetSecretAsync(_options.Value.StorageSharedAccessSignatureSecretName);

            _logger.LogInformation("Setting the SAS-based storage connection string in Key Vault.");
            var connectionString = $"AccountName={_options.Value.StorageAccountName};SharedAccessSignature={sas.Value}";
            await secretClient.SetSecretAsync(_options.Value.StorageConnectionStringSecretName, connectionString);

            _logger.LogInformation("Done updating the SAS-based storage connection string.");
        }

        private async Task RestartFunctionApps()
        {
            _logger.LogInformation(
                "Listing all function apps in subscription {SubscriptionId} and resource group {ResourceGroupName}.",
                _options.Value.HostSubscriptionId,
                _options.Value.HostResourceGroupName);

            var credential = SdkContext
                .AzureCredentialsFactory
                .FromSystemAssignedManagedServiceIdentity(MSIResourceType.AppService, AzureEnvironment.AzureGlobalCloud);

            var azure = Microsoft.Azure.Management.Fluent.Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .WithHttpClient(_httpClient)
                .Authenticate(credential)
                .WithSubscription(_options.Value.HostSubscriptionId);

            var functionApps = (await azure
                .AppServices
                .FunctionApps
                .ListByResourceGroupAsync(_options.Value.HostResourceGroupName))
                .ToList()
                .AsEnumerable();
            _logger.LogInformation("Found {Count} function apps.", functionApps.Count());

            // Restart the running app last, because this thread could terminate :)
            functionApps = functionApps.OrderBy(x => x.Name == _options.Value.HostAppName);

            foreach (var functionApp in functionApps)
            {
                _logger.LogInformation("Restarting function app {FunctionAppName}...", functionApp.Name);
                await functionApp.RestartAsync();
            }

            _logger.LogInformation("Done restarting the function apps.");
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task<bool> IsRunningAsync()
        {
            return Task.FromResult(false);
        }
    }
}
