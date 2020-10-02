using Knapcode.ExplorePackages.Logic.Worker.BlobStorage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGetPackageIdentity = NuGet.Packaging.Core.PackageIdentity;

namespace Knapcode.ExplorePackages.Logic.Worker.RunRealRestore
{
    public class RunRealRestoreProcessor : IMessageProcessor<RunRealRestoreMessage>
    {
        private readonly ProjectHelper _projectHelper;
        private readonly AppendResultStorageService _storageService;
        private readonly ServiceClientFactory _serviceClientFactory;
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

        private static HashSet<string> IgnoredPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
            ILogger<RunRealRestoreProcessor> logger)
        {
            _projectHelper = projectHelper;
            _storageService = storageService;
            _serviceClientFactory = serviceClientFactory;
            _logger = logger;
        }

        public async Task ProcessAsync(RunRealRestoreMessage message)
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
            if (!commandSucceeded && !result.OnlyNU1202 && !result.OnlyNU1213)
            {
                var account = _serviceClientFactory.GetStorageAccount();
                var client = account.CreateCloudBlobClient();
                var container = client.GetContainerReference(RunRealRestoreConstants.ContainerName);
                result.ErrorBlobPath = $"errors/{StorageUtility.GenerateDescendingId()}_{package.Id}_{package.Version.ToNormalizedString()}_{framework.GetShortFolderName()}.json";
                var blob = container.GetBlockBlobReference(result.ErrorBlobPath);
                blob.Properties.ContentType = "application/json";
                var errorBlob = new RunRealRestoreErrorResult { Result = result, CommandResults = _projectHelper.CommandResults };
                await blob.UploadTextAsync(JsonConvert.SerializeObject(errorBlob));
            }

            var storage = new AppendResultStorage(RunRealRestoreConstants.ContainerName, bucketCount: 1000);
            var bucketKey = $"{package.Id}/{packageVersion.ToNormalizedString()}".ToLowerInvariant();
            await _storageService.AppendAsync(storage, bucketKey, new[] { result });
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

                var restoreSucceeded = false;
                try
                {
                    _projectHelper.Restore(projectPath);
                    restoreSucceeded = true;
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Restore failed.");
                }

                // A build requires a successful restore.
                bool? buildSucceeded = null;
                if (restoreSucceeded)
                {
                    buildSucceeded = false;
                    try
                    {
                        _projectHelper.Build(projectPath);
                        buildSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(ex, "Build failed.");
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

                return new RealRestoreResult(timestamp, dotnetVersion, stopwatch.Elapsed, package, projectProfile, assetsFile, target, library)
                {
                    RestoreSucceeded = restoreSucceeded,
                    BuildSucceeded = buildSucceeded,
                };
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
