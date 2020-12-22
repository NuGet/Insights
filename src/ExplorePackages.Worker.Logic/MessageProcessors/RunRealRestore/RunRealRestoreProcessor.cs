using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;
using NuGetPackageIdentity = NuGet.Packaging.Core.PackageIdentity;

namespace Knapcode.ExplorePackages.Worker.RunRealRestore
{
    public class RunRealRestoreProcessor : IMessageProcessor<RunRealRestoreMessage>
    {
        private readonly ProjectHelper _projectHelper;
        private readonly AppendResultStorageService _storageService;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<RunRealRestoreProcessor> _logger;

        private const string ConsoleTemplate = "console";
        private const string ClassLibTemplate = "classlib";
        private static readonly IReadOnlyDictionary<string, string> FrameworkNameToTemplateName = new Dictionary<string, string>
        {
            { ".NETStandard", ClassLibTemplate },
        };

        private static readonly NuGetPackageIdentity CommonProjectTemplates31 = new NuGetPackageIdentity("Microsoft.DotNet.Common.ProjectTemplates.3.1", NuGetVersion.Parse("3.1.2"));
        private static readonly IReadOnlyDictionary<string, NuGetPackageIdentity> TemplateNameToPackage = new Dictionary<string, NuGetPackageIdentity>
        {
            { ConsoleTemplate, CommonProjectTemplates31 },
            { ClassLibTemplate, CommonProjectTemplates31 },
        };

        private static readonly HashSet<string> IgnoredPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // These packages are part of the SDK after netcoreapp3.0, therefore they will not appear in the assets
            // file, which messes up the analysis in this class.
            // https://github.com/dotnet/sdk/blob/44f381b62d466565639d51847c9127afbe7062a9/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.DefaultItems.targets#L120-L140
            "Microsoft.AspNetCore.App",
            "Microsoft.AspNetCore.All",
        };

        public RunRealRestoreProcessor(
            ProjectHelper projectHelper,
            AppendResultStorageService storageService,
            ServiceClientFactory serviceClientFactory,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<RunRealRestoreProcessor> logger)
        {
            _projectHelper = projectHelper;
            _storageService = storageService;
            _serviceClientFactory = serviceClientFactory;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(RunRealRestoreMessage message, int dequeueCount)
        {
            if (IgnoredPackageIds.Contains(message.Id))
            {
                _logger.LogWarning("Package {PackageId} ignored. No real restore will be run.", message.Id);
                return;
            }

            var packageVersion = NuGetVersion.Parse(message.Version);
            var package = new NuGetPackageIdentity(message.Id, packageVersion);
            var framework = NuGetFramework.Parse(message.Framework);

            if (!FrameworkNameToTemplateName.TryGetValue(framework.Framework, out var templateName))
            {
                templateName = ConsoleTemplate;
            }

            var templatePackage = TemplateNameToPackage[templateName];

            var projectProfile = new ProjectProfile(framework, templateName, templatePackage);
            var result = GetRealRestoreResult(package, projectProfile);

            var commandSucceeded = result.RestoreSucceeded && result.BuildSucceeded.GetValueOrDefault(false);
            var knownError = new[] { result.OnlyNU1202, result.OnlyNU1213, result.OnlyMSB3644 }.Any(x => x == true);

            if (!commandSucceeded && !knownError)
            {
                var account = _serviceClientFactory.GetStorageAccount();
                var client = account.CreateCloudBlobClient();
                var container = client.GetContainerReference(_options.Value.RunRealRestoreContainerName);
                result.ErrorBlobPath = $"errors/{StorageUtility.GenerateDescendingId()}_{package.Id}_{package.Version.ToNormalizedString()}_{framework.GetShortFolderName()}.json";
                var blob = container.GetBlockBlobReference(result.ErrorBlobPath);
                blob.Properties.ContentType = "application/json";
                var errorBlob = new RunRealRestoreErrorResult { Result = result, CommandResults = _projectHelper.CommandResults };
                await blob.UploadTextAsync(JsonConvert.SerializeObject(errorBlob));
            }

            var bucketKey = $"{package.Id}/{packageVersion.ToNormalizedString()}".ToLowerInvariant();
            await _storageService.AppendAsync(
                _options.Value.RunRealRestoreContainerName,
                _options.Value.AppendResultStorageBucketCount,
                bucketKey,
                new[] { result });
        }

        private RealRestoreResult GetRealRestoreResult(NuGetPackageIdentity package, ProjectProfile projectProfile)
        {
            var timestamp = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            var dotnetVersion = _projectHelper.GetDotnetVersion();

            var projectDir = Path.Combine(Path.GetTempPath(), "Knapcode.ExplorePackages", Guid.NewGuid().ToString());
            try
            {
                var projectPath = _projectHelper.ClearAndCreateProject(projectDir, projectProfile);

                _projectHelper.SetFramework(projectPath, projectProfile.Framework);
                _projectHelper.AddPackage(projectPath, package);

                var restoreResult = _projectHelper.Restore(projectPath);
                var restoreSucceeded = restoreResult.Succeeded;

                // A build requires a successful restore.
                bool? buildSucceeded = null;
                bool? onlyMSB3644 = null;
                string buildErrorCodes = null;
                if (restoreSucceeded)
                {
                    var buildResult = _projectHelper.Build(projectPath);
                    buildSucceeded = buildResult.Succeeded;

                    var buildErrorCodeCounts = Regex
                        .Matches(buildResult.Output, "error\\s+(\\w+\\d+)\\s*:", RegexOptions.IgnoreCase)
                        .Cast<Match>()
                        .GroupBy(x => x.Groups[1].Value.ToUpperInvariant())
                        .ToDictionary(x => x.Key, x => x.Count());

                    onlyMSB3644 = false;
                    if (buildErrorCodeCounts.Any())
                    {
                        buildErrorCodes = JsonConvert.SerializeObject(buildErrorCodeCounts);
                        onlyMSB3644 = !buildResult.Succeeded && buildErrorCodeCounts.Count == 1 && buildErrorCodeCounts.ContainsKey("MSB3644");
                    }
                }

                // An assets file is sometimes still produces when restore fails (N1202, for example).
                var assetsFile = _projectHelper.ReadAssetsFileOrNull(projectPath);
                LockFileTarget target = null;
                LockFileTargetLibrary library = null;
                if (restoreSucceeded && assetsFile != null)
                {
                    target = _projectHelper.GetMatchingTarget(assetsFile, projectProfile.Framework);
                    library = _projectHelper.GetMatchingLibrary(target, package);
                }

                return new RealRestoreResult(
                    timestamp,
                    dotnetVersion,
                    stopwatch.Elapsed,
                    package,
                    projectProfile,
                    restoreSucceeded,
                    buildSucceeded,
                    assetsFile,
                    target,
                    library,
                    buildErrorCodes,
                    onlyMSB3644);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(projectDir))
                    {
                        Directory.Delete(projectDir, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clean up test directory.");
                }
            }
        }
    }
}
