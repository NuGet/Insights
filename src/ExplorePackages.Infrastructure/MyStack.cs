using System.Text.RegularExpressions;
using Knapcode.ExplorePackages.Worker;
using Pulumi;
using Pulumi.Azure.AppInsights;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Core;
using Pulumi.Azure.Storage;

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

            var deploymentContainer = new Container("deployment", new ContainerArgs
            {
                StorageAccountName = storageAccount.Name,
            });

            var deploymentBlob = new Blob("Knapcode.ExplorerPackages.Worker", new BlobArgs
            {
                StorageAccountName = storageAccount.Name,
                StorageContainerName = deploymentContainer.Name,
                Type = "Block",
                Source = new FileArchive("../../artifacts/ExplorePackages/ExplorePackages.Worker/bin/Release/netcoreapp3.1/publish"),
            });

            var deploymentBlobUrl = SharedAccessSignature.SignedBlobReadUrl(deploymentBlob, storageAccount);

            var config = new Config();

            var defaultSettings = new ExplorePackagesWorkerSettings();

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
                    { "WEBSITE_RUN_FROM_PACKAGE", deploymentBlobUrl },
                    { "APPINSIGHTS_INSTRUMENTATIONKEY", appInsights.InstrumentationKey },
                    { "APPLICATIONINSIGHTS_CONNECTION_STRING", appInsights.ConnectionString },
                    { "FUNCTIONS_WORKER_RUNTIME", "dotnet" },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesSettings.StorageConnectionString)}", storageAccount.PrimaryConnectionString },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWorkerSettings.WorkerQueueName)}", defaultSettings.WorkerQueueName },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWorkerSettings.AppendResultStorageMode)}", config.Get(nameof(ExplorePackagesWorkerSettings.AppendResultStorageMode)) ?? defaultSettings.AppendResultStorageMode.ToString() },
                },
            });
        }
    }
}

