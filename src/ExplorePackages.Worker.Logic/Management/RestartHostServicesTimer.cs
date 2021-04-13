using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class RestartHostServicesTimer : ITimer
    {
        private readonly HttpClient _httpClient;
        private IOptions<ExplorePackagesWorkerSettings> _options;

        public RestartHostServicesTimer(HttpClient httpClient, IOptions<ExplorePackagesWorkerSettings> options)
        {
            _httpClient = httpClient;
            _options = options;
        }

        public string Name => "RestartHostServicesTimer";
        public TimeSpan Frequency => TimeSpan.FromHours(6);
        public bool AutoStart => true;
        public bool IsEnabled => _options.Value.RestartHostServices;

        public async Task ExecuteAsync()
        {
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
                .OrderBy(x => x.Name == _options.Value.HostResourceName); // restart the running app last, because this thread could terminate :)

            foreach (var functionApp in functionApps)
            {
                await functionApp.RestartAsync();
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
