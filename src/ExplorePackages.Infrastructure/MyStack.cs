using Pulumi;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.AppInsights;
using Pulumi.Azure.Core;
using Pulumi.Azure.Storage;
using System.Text.RegularExpressions;

namespace Knapcode.ExplorePackages
{
    public class MyStack : Stack
    {
        public MyStack()
        {
            var projectStack = Deployment.Instance.ProjectName + "-" + Deployment.Instance.StackName;
            var stackAlpha = Regex.Replace(Deployment.Instance.StackName, "[^a-z0-9]", string.Empty, RegexOptions.IgnoreCase);

            var resourceGroup = new ResourceGroup(projectStack);

            var storageAccount = new Account("explore" + stackAlpha.ToLowerInvariant(), new AccountArgs
            {
                ResourceGroupName = resourceGroup.Name,
                AccountReplicationType = "LRS",
                AccountTier = "Standard",
            });

            var appInsights = new Insights("ExplorePackages" + stackAlpha, new InsightsArgs
            {
                ResourceGroupName = resourceGroup.Name,
                ApplicationType = "web",
            });

            var workerPlan = new Plan("ExplorePackagesWorker" + stackAlpha, new PlanArgs
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

            var workerApp = new FunctionApp("ExplorePackagesWorker" + stackAlpha, new FunctionAppArgs
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

