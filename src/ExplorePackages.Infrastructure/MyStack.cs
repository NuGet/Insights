using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker;
using Pulumi;
using Pulumi.AzureAD;
using Pulumi.Azure.AppInsights;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Core;
using Pulumi.Azure.Storage;
using Config = Pulumi.Config;
using Knapcode.ExplorePackages.Website;
using System.Diagnostics;
using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;

namespace Knapcode.ExplorePackages
{
    public class MyStack : Stack
    {
        private readonly Config _config;
        private readonly string _projectStack;
        private readonly string _stackAlpha;
        private readonly ResourceGroup _resourceGroup;
        private readonly Account _storageAccount;
        private readonly Container _deploymentContainer;
        private readonly Insights _appInsights;

        public MyStack()
        {
            _config = new Config();
            _projectStack = Deployment.Instance.ProjectName + "-" + Deployment.Instance.StackName;
            _stackAlpha = Regex.Replace(Deployment.Instance.StackName, "[^a-z0-9]", string.Empty, RegexOptions.IgnoreCase);

            _resourceGroup = new ResourceGroup(_projectStack);

            _storageAccount = new Account("explore" + _stackAlpha.ToLowerInvariant(), new AccountArgs
            {
                ResourceGroupName = _resourceGroup.Name,
                AccountReplicationType = "LRS",
                AccountTier = "Standard",
            });

            _deploymentContainer = new Container("deployment", new ContainerArgs
            {
                StorageAccountName = _storageAccount.Name,
            });

            _appInsights = new Insights("ExplorePackages" + _stackAlpha, new InsightsArgs
            {
                ResourceGroupName = _resourceGroup.Name,
                ApplicationType = "web",
                DailyDataCapInGb = 0.5,
            });

            CreateWebsite();

            CreateWorker();
        }

        [Output]
        public Output<bool> AadAppUpdated { get; set; }

        private void CreateWebsite()
        {
            Output<string> planId;
            var configuredPlanId = _config.Get("WebsitePlanId");
            if (configuredPlanId != null)
            {
                planId = Output<string>.Create(Task.FromResult(configuredPlanId));
            }
            else
            {
                var plan = new Plan("ExplorePackagesWebsite" + _stackAlpha, new PlanArgs
                {
                    ResourceGroupName = _resourceGroup.Name,
                    Kind = "App",
                    Sku = new PlanSkuArgs
                    {
                        Tier = "Basic",
                        Size = "B1",
                    },
                });
                planId = plan.Id;
            }

            var deploymentBlob = new Blob("Knapcode.ExplorerPackages.Website", new BlobArgs
            {
                StorageAccountName = _storageAccount.Name,
                StorageContainerName = _deploymentContainer.Name,
                Type = "Block",
                Source = new FileArchive("../../artifacts/ExplorePackages/ExplorePackages.Website/bin/Release/netcoreapp3.1/publish"),
            });

            var deploymentBlobUrl = SharedAccessSignature.SignedBlobReadUrl(deploymentBlob, _storageAccount);

            var aadApp = new Application("Knapcode.ExplorePackages-" + _stackAlpha);

            var appService = new AppService("ExplorePackagesWebsite" + _stackAlpha, new AppServiceArgs
            {
                ResourceGroupName = _resourceGroup.Name,
                AppServicePlanId = planId,
                ClientAffinityEnabled = false,
                HttpsOnly = true,
                SiteConfig = new AppServiceSiteConfigArgs
                {
                    WebsocketsEnabled = true,
                },
                // Workaround for a bug. Source: https://github.com/pulumi/pulumi-azure/issues/740#issuecomment-734054001
                Logs = new AppServiceLogsArgs
                {
                    HttpLogs = new AppServiceLogsHttpLogsArgs
                    {
                        FileSystem = new AppServiceLogsHttpLogsFileSystemArgs
                        {
                            RetentionInDays = 1,
                            RetentionInMb = 25,
                        },
                    },
                },
                AppSettings = new InputMap<string>
                {
                    { "WEBSITE_RUN_FROM_PACKAGE", deploymentBlobUrl },
                    { "APPINSIGHTS_INSTRUMENTATIONKEY", _appInsights.InstrumentationKey },
                    { "APPLICATIONINSIGHTS_CONNECTION_STRING", _appInsights.ConnectionString },
                    { "ApplicationInsightsAgent_EXTENSION_VERSION", "~2" },
                    { "AzureAd:Instance", "https://login.microsoftonline.com/" },
                    { "AzureAd:ClientId", aadApp.ApplicationId },
                    { "AzureAd:TenantId", "common" },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesSettings.StorageConnectionString)}", _storageAccount.PrimaryConnectionString },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWebsiteSettings.AllowedUsers)}:0:{nameof(AllowedUser.TenantId)}", "9188040d-6c67-4c5b-b112-36a304b66dad" },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWebsiteSettings.AllowedUsers)}:0:{nameof(AllowedUser.ObjectId)}", "00000000-0000-0000-1325-2c8418ebab3b" },
                },
            });

            AadAppUpdated = aadApp.ApplicationId.Apply(clientId =>
            {
                var objectId = ExecuteJson("cmd", "/c", "az", "ad", "app", "show", "--id", clientId).Value<string>("objectId");

                return appService.DefaultSiteHostname.Apply(defaultSiteHostname =>
                {
                    if (!Deployment.Instance.IsDryRun)
                    {
                        Execute("cmd", "/c", "az", "rest",
                            "--method", "PATCH",
                            "--headers", "Content-Type=application/json",
                            "--uri", $"https://graph.microsoft.com/v1.0/applications/{objectId}",
                            "--body", JsonConvert.SerializeObject(new
                            {
                                api = new
                                {
                                    requestedAccessTokenVersion = 2,
                                },
                                signInAudience = "AzureADandPersonalMicrosoftAccount",
                                web = new
                                {
                                    redirectUris = new[]
                                    {
                                        $"https://{defaultSiteHostname}/signin-oidc",
                                    },
                                    logoutUrl = $"https://{defaultSiteHostname}/signout-oidc",
                                },
                            }));
                    }

                    return true;
                });
            });
        }

        private string Execute(string fileName, params string[] arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();
                process.StartInfo.FileName = fileName;
                foreach (var argument in arguments)
                {
                    process.StartInfo.ArgumentList.Add(argument);
                }
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                var output = new ConcurrentQueue<(bool isOutput, string data)>();
                process.OutputDataReceived += (sender, args) => output.Enqueue((true, args.Data));
                process.ErrorDataReceived += (sender, args) => output.Enqueue((false, args.Data));
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Command failed: {fileName} {string.Join(' ', arguments)}" +
                        System.Environment.NewLine +
                        string.Join(System.Environment.NewLine, output.Select(x => x.data)));
                }

                return string.Join(System.Environment.NewLine, output.Where(x => x.isOutput).Select(x => x.data));
            }
        }

        private JToken ExecuteJson(string fileName, params string[] arguments)
        {
            var output = Execute(fileName, arguments);
            return JToken.Parse(output);
        }

        private void CreateWorker()
        {
            var plan = new Plan("ExplorePackagesWorker" + _stackAlpha, new PlanArgs
            {
                ResourceGroupName = _resourceGroup.Name,
                Kind = "FunctionApp",
                Sku = new PlanSkuArgs
                {
                    Tier = "Dynamic",
                    Size = "Y1",
                },
            });

            var deploymentBlob = new Blob("Knapcode.ExplorerPackages.Worker", new BlobArgs
            {
                StorageAccountName = _storageAccount.Name,
                StorageContainerName = _deploymentContainer.Name,
                Type = "Block",
                Source = new FileArchive("../../artifacts/ExplorePackages/ExplorePackages.Worker/bin/Release/netcoreapp3.1/publish"),
            });

            var deploymentBlobUrl = SharedAccessSignature.SignedBlobReadUrl(deploymentBlob, _storageAccount);

            var defaultSettings = new ExplorePackagesWorkerSettings();
            var leafScanFullName = typeof(CatalogLeafScanMessage).FullName;

            var app = new FunctionApp("ExplorePackagesWorker" + _stackAlpha, new FunctionAppArgs
            {
                ResourceGroupName = _resourceGroup.Name,
                AppServicePlanId = plan.Id,
                StorageAccountName = _storageAccount.Name,
                StorageAccountAccessKey = _storageAccount.PrimaryAccessKey,
                EnableBuiltinLogging = false,
                Version = "~3",
                ClientAffinityEnabled = false,
                HttpsOnly = true,
                AppSettings = new InputMap<string>
                {
                    { "WEBSITE_RUN_FROM_PACKAGE", deploymentBlobUrl },
                    { "APPINSIGHTS_INSTRUMENTATIONKEY", _appInsights.InstrumentationKey },
                    { "APPLICATIONINSIGHTS_CONNECTION_STRING", _appInsights.ConnectionString },
                    { "FUNCTIONS_WORKER_RUNTIME", "dotnet" },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesSettings.StorageConnectionString)}", _storageAccount.PrimaryConnectionString },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWorkerSettings.WorkerQueueName)}", defaultSettings.WorkerQueueName },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWorkerSettings.AppendResultStorageMode)}", _config.Get(nameof(ExplorePackagesWorkerSettings.AppendResultStorageMode)) ?? defaultSettings.AppendResultStorageMode.ToString() },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWorkerSettings.MessageBatchSizes)}:{leafScanFullName}", _config.Get("CatalogLeafScanBatchSize") ?? "1" },
                },
            });
        }
    }
}

