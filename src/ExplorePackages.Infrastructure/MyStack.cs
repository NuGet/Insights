using Pulumi;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.AppInsights;
using Pulumi.Azure.Core;
using Pulumi.Azure.Storage;

namespace Knapcode.ExplorePackages
{
    public class MyStack : Stack
    {
        public MyStack()
        {
            var resourceGroup = new ResourceGroup(Deployment.Instance.ProjectName + "-" + Deployment.Instance.StackName);

            var storageAccount = new Account("explorepackages", new AccountArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AccountReplicationType = "LRS",
                AccountTier = "Standard",
            });

            var appInsights = new Insights("explorepackages", new InsightsArgs
            {
                ResourceGroupName = resourceGroup.Name,
                ApplicationType = "web",
            });

            var workerPlan = new Plan("ExplorePackagesWorker", new PlanArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Kind = "FunctionApp",
                Sku = new PlanSkuArgs
                {
                    Tier = "Dynamic",
                    Size = "Y1",
                },
            });

            var config = new Config();

            var workerApp = new FunctionApp("ExplorePackagesWorker", new FunctionAppArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AppServicePlanId = workerPlan.Id,
                StorageAccountName = storageAccount.Name,
                StorageAccountAccessKey = storageAccount.PrimaryAccessKey,
                EnableBuiltinLogging = false,
                Version = "~3",
                AppSettings = new InputMap<string>
                {
                    { "APPINSIGHTS_INSTRUMENTATIONKEY", appInsights.InstrumentationKey },
                    { "APPLICATIONINSIGHTS_CONNECTION_STRING", appInsights.ConnectionString },
                    { "FUNCTIONS_WORKER_RUNTIME", "dotnet" },
                    { "Knapcode.ExplorePackages:StorageConnectionString", storageAccount.PrimaryConnectionString },
                    { "Knapcode.ExplorePackages:WorkerQueueName", "worker-queue" },
                    { "Knapcode.ExplorePackages:AppendResultStorageMode", config.Require("AppendResultStorageMode") },
                },
            });
        }
    }
}

