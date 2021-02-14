using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Website;
using Knapcode.ExplorePackages.Worker;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pulumi;
using Pulumi.Azure.AppInsights;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Core;
using Pulumi.Azure.Storage;
using Pulumi.AzureAD;
using Config = Pulumi.Config;

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
                DailyDataCapInGb = 0.2,
                RetentionInDays = 30,
            });

            CreateWebsite();

            CreateWorker();
        }

        [Output]
        public Output<string> WebsiteUrl { get; private set; }

        [Output]
        public Output<bool> AadAppUpdated { get; private set; }

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

            var aadApp = new Application("Knapcode.ExplorePackages-" + _stackAlpha, new ApplicationArgs
            {
                DisplayName = "Knapcode.ExplorePackages-" + _stackAlpha,
            });

            var configuredSettings = _config.GetObject<ExplorePackagesWebsiteSettings>("AppSettings");

            var appSettings = new InputMap<string>
            {
                { "WEBSITE_RUN_FROM_PACKAGE", deploymentBlobUrl },
                { "APPINSIGHTS_INSTRUMENTATIONKEY", _appInsights.InstrumentationKey },
                { "APPLICATIONINSIGHTS_CONNECTION_STRING", _appInsights.ConnectionString },
                { "ApplicationInsightsAgent_EXTENSION_VERSION", "~2" },
                { "AzureAd:Instance", "https://login.microsoftonline.com/" },
                { "AzureAd:ClientId", aadApp.ApplicationId },
                { "AzureAd:TenantId", "common" },
                { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesSettings.StorageConnectionString)}", _storageAccount.PrimaryConnectionString },
                { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWebsiteSettings.ShowAdminLink)}", configuredSettings.ShowAdminLink.ToString() },
            };

            for (var i = 0; i < configuredSettings.AllowedUsers.Count; i++)
            {
                var allowedUser = configuredSettings.AllowedUsers[i];
                appSettings.Add(
                    $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWebsiteSettings.AllowedUsers)}:{i}:{nameof(AllowedUser.HashedTenantId)}",
                    allowedUser.HashedTenantId);
                appSettings.Add(
                    $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWebsiteSettings.AllowedUsers)}:{i}:{nameof(AllowedUser.HashedObjectId)}",
                    allowedUser.HashedObjectId);
            }

            var appServiceArgs = new AppServiceArgs
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
                AppSettings = appSettings,
            };

            var appServiceName = _config.Get("AppServiceName");
            if (appServiceName != null)
            {
                appServiceArgs.Name = appServiceName;
            }

            var appService = new AppService("ExplorePackagesWebsite" + _stackAlpha, appServiceArgs);

            WebsiteUrl = appService.DefaultSiteHostname.Apply(defaultSiteHostname => $"https://{defaultSiteHostname}");

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
                                    homePageUrl = $"https://{defaultSiteHostname}",
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
                    { "AzureFunctionsJobHost__logging__LogLevel__Default", "Warning" },
                    // Workaround for a bug. Source: https://github.com/Azure/azure-functions-host/issues/5098#issuecomment-704206997
                    { "AzureWebJobsFeatureFlags", "EnableEnhancedScopes" },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesSettings.StorageConnectionString)}", _storageAccount.PrimaryConnectionString },
                    { $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWorkerSettings.WorkerQueueName)}", defaultSettings.WorkerQueueName },
                },
            });
        }
    }
}

