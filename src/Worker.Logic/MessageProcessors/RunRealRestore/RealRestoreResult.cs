using System;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.ProjectModel;
using NuGetPackageIdentity = NuGet.Packaging.Core.PackageIdentity;

namespace Knapcode.ExplorePackages.Worker.RunRealRestore
{
    public partial record RealRestoreResult : ICsvRecord
    {
        public RealRestoreResult()
        {
        }

        public RealRestoreResult(
            DateTimeOffset timestamp,
            string dotnetVersion,
            TimeSpan duration,
            NuGetPackageIdentity package,
            ProjectProfile projectProfile,
            bool restoreSucceeded,
            bool? buildSucceeded,
            LockFile assetsFile,
            LockFileTarget target,
            LockFileTargetLibrary library,
            string buildErrorCodes,
            bool? onlyMSB3644)
        {
            Timestamp = timestamp;
            DotnetVersion = dotnetVersion;
            Duration = duration;
            Id = package.Id;
            Version = package.Version.ToNormalizedString();
            LowerId = package.Id.ToLowerInvariant();
            Identity = $"{LowerId}/{Version.ToLowerInvariant()}";
            Framework = projectProfile.Framework.GetShortFolderName();
            Template = projectProfile.TemplateName;

            RestoreSucceeded = restoreSucceeded;
            BuildSucceeded = buildSucceeded;

            if (assetsFile != null)
            {
                TargetCount = assetsFile.Targets.Count;

                var logMessageCodes = assetsFile
                    .LogMessages
                    .Select(x => x.Code)
                    .GroupBy(x => x)
                    .ToDictionary(x => x.Key, x => x.Count());

                if (!restoreSucceeded && !assetsFile.LogMessages.Any())
                {
                    throw new ArgumentException("The restore failed but did not have any log messages.");
                }

                if (logMessageCodes.Any())
                {
                    RestoreLogMessageCodes = JsonConvert.SerializeObject(logMessageCodes.ToDictionary(x => x.Key.ToString(), x => x.Value));
                }

                OnlyNU1202 = logMessageCodes.Count == 1 && logMessageCodes.ContainsKey(NuGetLogCode.NU1202);
                OnlyNU1213 = logMessageCodes.Count == 1 && logMessageCodes.ContainsKey(NuGetLogCode.NU1213);
            }

            if (target != null)
            {
                LibraryCount = target.Libraries.Count;
            }

            if (library != null)
            {
                DependencyCount = library.Dependencies.Count;
                FrameworkAssemblyCount = library.FrameworkAssemblies.Count;
                FrameworkReferenceCount = library.FrameworkReferences.Count;
                RuntimeAssemblyCount = library.RuntimeAssemblies.Count;
                ResourceAssemblyCount = library.ResourceAssemblies.Count;
                CompileTimeAssemblyCount = library.CompileTimeAssemblies.Count;
                NativeLibraryCount = library.NativeLibraries.Count;
                BuildCount = library.Build.Count;
                BuildMultiTargetingCount = library.BuildMultiTargeting.Count;
                ContentFileCount = library.ContentFiles.Count;
                RuntimeTargetCount = library.RuntimeTargets.Count;
                ToolAssemblyCount = library.ToolsAssemblies.Count;
                EmbedAssemblyCount = library.EmbedAssemblies.Count;
            }

            BuildErrorCodes = buildErrorCodes;
            OnlyMSB3644 = onlyMSB3644;
        }

        public DateTimeOffset Timestamp { get; set; }
        public string DotnetVersion { get; set; }
        public TimeSpan Duration { get; set; }

        public string LowerId { get; set; }

        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }
        public string Framework { get; set; }
        public string Template { get; set; }

        public int? TargetCount { get; set; }
        public int? LibraryCount { get; set; }

        public bool RestoreSucceeded { get; set; }
        public bool? BuildSucceeded { get; set; }

        public int? DependencyCount { get; set; }
        public int? FrameworkAssemblyCount { get; set; }
        public int? FrameworkReferenceCount { get; set; }
        public int? RuntimeAssemblyCount { get; set; }
        public int? ResourceAssemblyCount { get; set; }
        public int? CompileTimeAssemblyCount { get; set; }
        public int? NativeLibraryCount { get; set; }
        public int? BuildCount { get; set; }
        public int? BuildMultiTargetingCount { get; set; }
        public int? ContentFileCount { get; set; }
        public int? RuntimeTargetCount { get; set; }
        public int? ToolAssemblyCount { get; set; }
        public int? EmbedAssemblyCount { get; set; }

        public string ErrorBlobPath { get; set; }

        [KustoType("dynamic")]
        public string RestoreLogMessageCodes { get; set; }

        public bool? OnlyNU1202 { get; set; }
        public bool? OnlyNU1213 { get; set; }

        [KustoType("dynamic")]
        public string BuildErrorCodes { get; set; }

        public bool? OnlyMSB3644 { get; set; }
    }
}
