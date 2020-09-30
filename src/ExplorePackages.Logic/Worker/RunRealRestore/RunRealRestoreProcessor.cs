using Knapcode.ExplorePackages.Logic.Worker.BlobStorage;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.Versioning;
using System;
using System.IO;
using System.Threading.Tasks;
using NuGetPackageIdentity = NuGet.Packaging.Core.PackageIdentity;

namespace Knapcode.ExplorePackages.Logic.Worker.RunRealRestore
{
    public class RunRealRestoreProcessor : IMessageProcessor<RunRealRestoreMessage>
    {
        private readonly ProjectHelper _projectHelper;
        private readonly AppendResultStorageService _storageService;
        private readonly ILogger<RunRealRestoreProcessor> _logger;

        public RunRealRestoreProcessor(
            ProjectHelper projectHelper,
            AppendResultStorageService storageService,
            ILogger<RunRealRestoreProcessor> logger)
        {
            _projectHelper = projectHelper;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task ProcessAsync(RunRealRestoreMessage message)
        {
            var packageVersion = NuGetVersion.Parse(message.Version);
            var package = new NuGetPackageIdentity(message.Id, packageVersion);
            var framework = NuGetFramework.Parse(message.Framework);
            var templatePackageVersion = NuGetVersion.Parse(message.TemplatePackageVersion);
            var projectProfile = new ProjectProfile(framework, message.TemplateName, message.TemplatePackageId, templatePackageVersion);
            var result = GetRealRestoreResult(package, projectProfile);

            var storage = new AppendResultStorage(RunRealRestoreConstants.ContainerName, bucketCount: 1);
            var bucketKey = $"{package.Id}/{packageVersion.ToNormalizedString()}".ToLowerInvariant();
            await _storageService.AppendAsync(storage, bucketKey, new[] { result });
        }

        private RealRestoreResult GetRealRestoreResult(NuGetPackageIdentity package, ProjectProfile projectProfile)
        {
            var projectDir = Path.Combine(Path.GetTempPath(), "Knapcode.ExplorePackages", Guid.NewGuid().ToString());
            try
            {
                var timestamp = DateTimeOffset.UtcNow;
                var dotnetVersion = _projectHelper.GetDotnetVersion();

                var projectPath = _projectHelper.ClearAndCreateProject(projectDir, projectProfile);

                _projectHelper.SetFramework(projectPath, projectProfile.Framework);
                _projectHelper.AddPackage(projectPath, package);

                try
                {
                    _projectHelper.Restore(projectPath);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Restore failed.");
                    return new RealRestoreResult(timestamp, dotnetVersion, package, projectProfile);
                }

                try
                {
                    _projectHelper.Build(projectPath);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Build failed.");
                    return new RealRestoreResult(timestamp, dotnetVersion, package, projectProfile) { RestoreSucceeded = true };
                }

                var assetsFile = _projectHelper.ReadAssetsFile(projectPath);
                var target = _projectHelper.GetMatchingTarget(assetsFile, projectProfile.Framework);
                var library = _projectHelper.GetMatchingLibrary(target, package);

                return new RealRestoreResult(timestamp, dotnetVersion, package, projectProfile, assetsFile, target, library);
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
