using System;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Logic.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new HostBuilder()
                .ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.AddConsole();
                })
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddExplorePackages();
                    serviceCollection.AddExplorePackagesSettings<Program>();

                    serviceCollection.AddTransient<StorageAccountProvider, ExplorePackagesStorageAccountProvider>();
                    serviceCollection.AddTransient<PackageQueryCommand>();
                    serviceCollection.AddScoped<CollectorMessageEnqueuer>();
                    serviceCollection.AddTransient<IMessageEnqueuer>(s => s.GetRequiredService<CollectorMessageEnqueuer>());
                })
                .ConfigureWebJobs(webJobsBuilder =>
                {
                    webJobsBuilder.AddAzureStorageCoreServices();
                    webJobsBuilder.AddAzureStorage(
                        configureQueues: o =>
                        {
                            o.MaxPollingInterval = TimeSpan.FromSeconds(5);
                        });
                });

            using (var host = builder.Build())
            {
                host.Run();
            }
        }
    }
}
