using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Website;
using Knapcode.ExplorePackages.Worker;
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
        private static readonly HttpClient _httpClient = new HttpClient();

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

            _appInsights = new Insights("ExplorePackages-" + _stackAlpha + "-", new InsightsArgs
            {
                ResourceGroupName = _resourceGroup.Name,
                ApplicationType = "web",
                DailyDataCapInGb = 0.2,
                RetentionInDays = 30,
            });

            CreateWebsite();

            CreateWorkers(_config.GetInt32("WorkerCount") ?? 1);
        }

        [Output]
        public Output<bool> RestartedWebsite { get; private set; }

        [Output]
        public Output<string> WebsiteUrl { get; private set; }

        [Output]
        public Output<bool> RestartedFunctionApps { get; private set; }

        [Output]
        public Output<ImmutableArray<string>> WorkerUrls { get; private set; }

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
                var plan = new Plan("ExplorePackagesWebsite-" + _stackAlpha + "-", new PlanArgs
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
                Source = new FileArchive("../../artifacts/ExplorePackages/ExplorePackages.Website/bin/Release/net5.0/publish"),
            });

            var deploymentBlobUrl = SharedAccessSignature.SignedBlobReadUrl(deploymentBlob, _storageAccount);

            var aadApp = new Application("Knapcode.ExplorePackages-" + _stackAlpha, new ApplicationArgs
            {
                DisplayName = "Knapcode.ExplorePackages-" + _stackAlpha,
            });

            var appSettings = new InputMap<string>
            {
                { "WEBSITE_RUN_FROM_PACKAGE", deploymentBlobUrl },
                { "APPINSIGHTS_INSTRUMENTATIONKEY", _appInsights.InstrumentationKey },
                { "APPLICATIONINSIGHTS_CONNECTION_STRING", _appInsights.ConnectionString },
                { "ApplicationInsightsAgent_EXTENSION_VERSION", "~2" },
                { "AzureAd:Instance", "https://login.microsoftonline.com/" },
                { "AzureAd:ClientId", aadApp.ApplicationId },
                { "AzureAd:TenantId", "common" },
            };

            AddWebsiteSettings(appSettings);

            var appServiceArgs = new AppServiceArgs
            {
                ResourceGroupName = _resourceGroup.Name,
                AppServicePlanId = planId,
                ClientAffinityEnabled = false,
                HttpsOnly = true,
                SiteConfig = new AppServiceSiteConfigArgs
                {
                    WebsocketsEnabled = true,
                    DotnetFrameworkVersion = "v5.0",
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

            var appService = new AppService("ExplorePackagesWebsite-" + _stackAlpha + "-", appServiceArgs);

            var aadAppUpdate = new Pulumi.Knapcode.PrepareAppForWebSignIn("PrepareAppForWebSignIn", new Pulumi.Knapcode.PrepareAppForWebSignInArgs
            {
                ObjectId = aadApp.ObjectId,
                HostName = appService.DefaultSiteHostname,
            });

            RestartedWebsite = _resourceGroup.Name.Apply(resourceGroupName =>
            {
                return appService.Name.Apply(name =>
                {
                    if (!Deployment.Instance.IsDryRun)
                    {
                        Console.WriteLine($"Restarting {name}...");
                        Execute("cmd", "/c", "az", "webapp", "restart", "--resource-group", resourceGroupName, "--name", name);
                        Console.WriteLine($"Done restarting {name}.");
                    }

                    return true;
                });
            });

            WebsiteUrl = appService.DefaultSiteHostname.Apply(x => WarmUpAsync($"https://{x}"));
        }

        private void CreateWorkers(int count)
        {
            var deploymentBlob = new Blob("Knapcode.ExplorerPackages.Worker", new BlobArgs
            {
                StorageAccountName = _storageAccount.Name,
                StorageContainerName = _deploymentContainer.Name,
                Type = "Block",
                Source = new FileArchive("../../artifacts/ExplorePackages/ExplorePackages.Worker/bin/Release/netcoreapp3.1/publish"),
            });

            var deploymentBlobUrl = SharedAccessSignature.SignedBlobReadUrl(deploymentBlob, _storageAccount);

            var plan = new Plan("ExplorePackagesWorker-" + _stackAlpha + "-", new PlanArgs
            {
                ResourceGroupName = _resourceGroup.Name,
                Kind = "FunctionApp",
                Sku = new PlanSkuArgs
                {
                    Tier = "Dynamic",
                    Size = "Y1",
                },
            });

            var functionApps = new List<FunctionApp>();
            for (var instance = 0; instance < count; instance++)
            {
                functionApps.Add(CreateWorker(deploymentBlobUrl, instance, plan.Id));
            }

            RestartedFunctionApps = _resourceGroup.Name.Apply(resourceGroupName =>
            {
                return Output
                    .All(functionApps
                        .Select(x => x.Name.Apply(name =>
                        {
                            if (!Deployment.Instance.IsDryRun)
                            {
                                Console.WriteLine($"Restarting {name}...");
                                Execute("cmd", "/c", "az", "functionapp", "restart", "--resource-group", resourceGroupName, "--name", name);
                                Console.WriteLine($"Done restarting {name}.");
                            }

                            return true;
                        })))
                    .Apply(restarted => restarted.All(x => x));
            });

            WorkerUrls = Output.All(functionApps.Select(x => x.DefaultHostname.Apply(y => WarmUpAsync($"https://{y}"))));
        }

        private FunctionApp CreateWorker(Output<string> deploymentBlobUrl, int instance, Output<string> planId)
        {
            var appSettings = new InputMap<string>
            {
                { "WEBSITE_RUN_FROM_PACKAGE", deploymentBlobUrl },
                { "APPINSIGHTS_INSTRUMENTATIONKEY", _appInsights.InstrumentationKey },
                { "APPLICATIONINSIGHTS_CONNECTION_STRING", _appInsights.ConnectionString },
                { "FUNCTIONS_WORKER_RUNTIME", "dotnet" },
                { "AzureFunctionsJobHost__logging__LogLevel__Default", "Warning" },
                // Workaround for a bug. Source: https://github.com/Azure/azure-functions-host/issues/5098#issuecomment-704206997
                { "AzureWebJobsFeatureFlags", "EnableEnhancedScopes" },
            };

            AddWorkerSettings(appSettings);

            var maxWorkerScaleOut = _config.GetInt32("MaxWorkerScaleOut");
            if (maxWorkerScaleOut.HasValue)
            {
                appSettings.Add("WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT", maxWorkerScaleOut.Value.ToString());
            }

            var workerQueueBatchSize = _config.GetInt32("WorkerQueueBatchSize");
            if (workerQueueBatchSize.HasValue)
            {
                appSettings.Add("AzureFunctionsJobHost__extensions__queues__batchSize", workerQueueBatchSize.Value.ToString());
            }

            var app = new FunctionApp("ExplorePackagesWorker-" + _stackAlpha + "-" + instance + "-", new FunctionAppArgs
            {
                ResourceGroupName = _resourceGroup.Name,
                AppServicePlanId = planId,
                StorageAccountName = _storageAccount.Name,
                StorageAccountAccessKey = _storageAccount.PrimaryAccessKey,
                EnableBuiltinLogging = false,
                Version = "~3",
                ClientAffinityEnabled = false,
                HttpsOnly = true,
                AppSettings = appSettings,
            });

            return app;
        }

        private void AddWorkerSettings(InputMap<string> appSettings, bool skipMoveTempToHome = false)
        {
            var configuredSettings = DeserializeConfig<ExplorePackagesWorkerSettings>("WorkerSettings");

            appSettings.Add(
                $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesSettings.StorageConnectionString)}",
                _storageAccount.PrimaryConnectionString);

            appSettings.Add(
               $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWorkerSettings.WorkerQueueName)}",
               configuredSettings.WorkerQueueName);

            if (!skipMoveTempToHome)
            {
                appSettings.Add(
                   $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWorkerSettings.MoveTempToHome)}",
                   configuredSettings.MoveTempToHome.ToString());
            }

            var i = 0;
            foreach (var driver in configuredSettings.DisabledDrivers)
            {
                appSettings.Add(
                    $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWorkerSettings.DisabledDrivers)}:{i}",
                    driver.ToString());
                i++;
            }
        }

        private void AddWebsiteSettings(InputMap<string> appSettings)
        {
            AddWorkerSettings(appSettings, skipMoveTempToHome: true);

            var configuredSettings = DeserializeConfig<ExplorePackagesWebsiteSettings>("WebsiteSettings");

            appSettings.Add(
                $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWorkerSettings.MoveTempToHome)}",
                configuredSettings.MoveTempToHome.ToString());

            appSettings.Add(
               $"{ExplorePackagesSettings.DefaultSectionName}:{nameof(ExplorePackagesWebsiteSettings.ShowAdminLink)}",
               configuredSettings.ShowAdminLink.ToString());

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
        }

        private T DeserializeConfig<T>(string key)
        {
            using var jsonDocument = _config.GetObject<JsonDocument>(key);

            if (jsonDocument == null)
            {
                return Activator.CreateInstance<T>();
            }

            var json = jsonDocument.RootElement.GetRawText();
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonStringEnumConverter(),
                }
            });
        }

        private async Task<string> WarmUpAsync(string url)
        {
            if (!Deployment.Instance.IsDryRun)
            {
                int attempt = 0;
                const int maxAttempts = 5;
                while (true)
                {
                    attempt++;

                    if (attempt > 1)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }

                    try
                    {
                        Console.WriteLine($"Warming up {url}... (attempt {attempt})");
                        using var response = await _httpClient.GetAsync(url);
                        Console.WriteLine($"URL {url} returned '{(int)response.StatusCode} {response.ReasonPhrase}'. (attempt {attempt})");
                        response.EnsureSuccessStatusCode();
                        break;
                    }
                    catch when (attempt < maxAttempts)
                    {
                        Console.WriteLine($"URL {url} threw an exception. Trying again. (attempt {attempt})");
                    }
                }
            }

            return url;
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
    }
}

