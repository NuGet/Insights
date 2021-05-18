// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker
{
    public class UpdateSecretsTimer : ITimer
    {
        private static readonly ConcurrentBag<Task> RefreshTasks = new ConcurrentBag<Task>();

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly HttpClient _httpClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<UpdateSecretsTimer> _logger;

        public UpdateSecretsTimer(
            ServiceClientFactory serviceClientFactory,
            HttpClient httpClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<UpdateSecretsTimer> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _httpClient = httpClient;
            _options = options;
            _logger = logger;
        }

        public string Name => "UpdateSecrets";
        public bool AutoStart => true;
        public int Order => int.MaxValue;

        public TimeSpan Frequency
        {
            get
            {
                if (!_options.Value.StorageSharedAccessSignatureDuration.HasValue)
                {
                    // This is an arbitrary value since the timer won't run when the duration is null. This value does
                    // not matter.
                    return TimeSpan.FromDays(1);
                }

                var partialDuration = _options.Value.StorageSharedAccessSignatureDuration.Value / 3;

                // Run at least once every 2 days. This value picked as twice the guaranteed reload interval documented
                // Azure App Service Key Vault references. We'll see how this goes.
                // See: https://docs.microsoft.com/en-us/azure/app-service/app-service-key-vault-references#rotation
                if (partialDuration > TimeSpan.FromDays(2))
                {
                    return TimeSpan.FromDays(2);
                }

                return partialDuration;
            }
        }

        public bool IsEnabled => _options.Value.KeyVaultName != null
                    && _options.Value.StorageSharedAccessSignatureSecretName != null
                    && _options.Value.StorageSharedAccessSignatureDuration != null
                    && _options.Value.StorageConnectionStringSecretName != null
                    && _options.Value.HostSubscriptionId != null
                    && _options.Value.HostResourceGroupName != null
                    && _options.Value.HostAppName != null;

        public async Task<bool> ExecuteAsync()
        {
            // Update the storage connection with the latest SAS token.
            await UpdateStorageConnectionStringAsync();

            // Await refresh tasks from previous executions.
            await AwaitPreviousRefreshTasks();

            // We will ensure any Key Vault references used in the config are reloaded.
            await EnsureSecretsReloadAsync();

            return true;
        }

        private async Task UpdateStorageConnectionStringAsync()
        {
            _logger.LogInformation("Getting the existing connection string secret.");
            var secretClient = await _serviceClientFactory.GetKeyVaultSecretClientAsync();
            KeyVaultSecret connectionStringSecret = await secretClient.GetSecretAsync(_options.Value.StorageConnectionStringSecretName);
            if (connectionStringSecret.Properties.ExpiresOn.HasValue)
            {
                var untilExpires = connectionStringSecret.Properties.ExpiresOn.Value - DateTimeOffset.UtcNow;
                if (untilExpires > Frequency)
                {
                    _logger.LogInformation("The connection string doesn't expire for another {RemainingHours:F2} hours. It will not be updated.", untilExpires.TotalHours);
                    return;
                }
            }

            _logger.LogInformation("Getting the latest SAS token from Key Vault.");
            var now = DateTimeOffset.UtcNow;
            KeyVaultSecret sasSecret = await secretClient.GetSecretAsync(_options.Value.StorageSharedAccessSignatureSecretName);
            var sasExpires = StorageUtility.GetSasExpiry(sasSecret.Value);
            var sasRemaining = sasExpires - now;
            _logger.LogInformation("The SAS has {RemainingHours:F2} hours remaining.", sasRemaining.TotalHours);

            _logger.LogInformation("Setting the SAS-based storage connection string in Key Vault.");
            var connectionString = $"AccountName={_options.Value.StorageAccountName};SharedAccessSignature={sasSecret.Value};DefaultEndpointsProtocol=https;EndpointSuffix=core.windows.net";
            await secretClient.SetSecretAsync(new KeyVaultSecret(_options.Value.StorageConnectionStringSecretName, connectionString)
            {
                Properties =
                {
                    ExpiresOn = sasExpires,
                    Tags = { { "set-by", "timer" } },
                }
            });

            _logger.LogInformation("Done updating the SAS-based storage connection string.");
        }

        private async Task AwaitPreviousRefreshTasks()
        {
            while (RefreshTasks.TryTake(out var previous))
            {
                _logger.LogInformation("Ensuring a previous refresh task is awaited.");
                try
                {
                    await previous;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Previous refresh task failed.");
                }
            }
        }

        private async Task EnsureSecretsReloadAsync()
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

            // Process the running app last, because this thread could terminate :)
            var otherFunctionApps = functionApps.Where(x => x.Name != _options.Value.HostAppName);
            var thisFunctionApp = functionApps.Where(x => x.Name == _options.Value.HostAppName);

            _logger.LogInformation("Starting the refresh of function app Key Vault references.");
            await Task.WhenAll(otherFunctionApps.Select(x => RefreshAsync(credential, x, delay: TimeSpan.Zero)));

            // Don't await the refresh of the current app, to allow the completion of this timer. This is not a great idea.
            foreach (var functionApp in thisFunctionApp)
            {
                RefreshTasks.Add(RefreshAsync(credential, functionApp, delay: TimeSpan.FromSeconds(30)));
            }

            _logger.LogInformation("Done starting the refresh of function app Key Vault references.");
        }

        private async Task RefreshAsync(AzureCredentials credential, IFunctionApp functionApp, TimeSpan delay)
        {
            await Task.Delay(delay);

            try
            {
                // We don't use connection strings in this app so we can safely set the connection string object to an
                // empty object as a way to force Azure App Service to reload the Key Vault references.
                // See: https://docs.microsoft.com/en-us/azure/app-service/app-service-key-vault-references#rotation
                _logger.LogInformation("Clearing connections strings on function app {FunctionAppName} to reload secrets...", functionApp.Name);
                var url = new Uri(new Uri(credential.Environment.ResourceManagerEndpoint), functionApp.Id + "/config/connectionstrings?api-version=2019-08-01");
                using (var request = new HttpRequestMessage(HttpMethod.Put, url))
                {
                    request.Content = new StringContent("{\"properties\":{}}", Encoding.UTF8, "application/json");
                    await credential.ProcessHttpRequestAsync(request, default);

                    using var response = await _httpClient.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Clearing the connection strings on function app {FunctionAppName} failed.", functionApp.Name);
                throw;
            }
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
