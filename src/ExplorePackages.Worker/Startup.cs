using Azure.Messaging.ServiceBus;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Logic.Worker;
using Knapcode.ExplorePackages.Worker;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Knapcode.ExplorePackages.Worker
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder
                .Services
                .AddOptions<ExplorePackagesSettings>()
                .Configure<IConfiguration>((settings, configuration) =>
                {
                    configuration.GetSection(ExplorePackagesSettings.DefaultSectionName).Bind(settings);
                });

            builder.Services.AddExplorePackages();

            builder.Services.AddSingleton<IQueueProcessorFactory, UnencodedQueueProcessorFactory>();

            builder.Services.AddScoped<TargetableRawMessageEnqueuer>();
            builder.Services.AddScoped<QueueStorageEnqueuer>();
            builder.Services.AddScoped<OldServiceBusEnqueuer>();
            builder.Services.AddScoped<NewServiceBusEnqueuer>();
            builder.Services.AddScoped<ExternalWorkerQueueFactory>();
            builder.Services.AddScoped<IWorkerQueueFactory>(x => x.GetRequiredService<ExternalWorkerQueueFactory>());
            builder.Services.AddScoped<ServiceClientFactory>();

            builder.Services.AddSingleton(x =>
            {
                var settings = x.GetRequiredService<IOptionsSnapshot<ExplorePackagesSettings>>();
                return new ServiceBusClient(settings.Value.ServiceBusConnectionString);
            });

            builder.Services.AddSingleton(x =>
            {
                var client = x.GetRequiredService<ServiceBusClient>();
                return client.CreateSender("queue");
            });

            builder.Services.AddTransient<IRawMessageEnqueuer>(x => x.GetRequiredService<TargetableRawMessageEnqueuer>());
        }
    }
}
