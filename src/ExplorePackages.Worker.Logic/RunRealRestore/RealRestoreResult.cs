using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.ProjectModel;
using System;
using System.Linq;
using NuGetPackageIdentity = NuGet.Packaging.Core.PackageIdentity;

namespace Knapcode.ExplorePackages.Worker.RunRealRestore
{
    public partial class RealRestoreResult : ICsvWritable
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

        [Index(0)] public DateTimeOffset Timestamp { get; set; }
        [Index(1)] public string DotnetVersion { get; set; }
        [Index(2)] public TimeSpan Duration { get; set; }

        [Index(3)] public string Id { get; set; }
        [Index(4)] public string Version { get; set; }
        [Index(5)] public string Framework { get; set; }
        [Index(6)] public string Template { get; set; }

        [Index(7)] public int? TargetCount { get; set; }
        [Index(8)] public int? LibraryCount { get; set; }

        [Index(9)] public bool RestoreSucceeded { get; set; }
        [Index(10)] public bool? BuildSucceeded { get; set; }

        [Index(11)] public int? DependencyCount { get; set; }
        [Index(12)] public int? FrameworkAssemblyCount { get; set; }
        [Index(13)] public int? FrameworkReferenceCount { get; set; }
        [Index(14)] public int? RuntimeAssemblyCount { get; set; }
        [Index(15)] public int? ResourceAssemblyCount { get; set; }
        [Index(16)] public int? CompileTimeAssemblyCount { get; set; }
        [Index(17)] public int? NativeLibraryCount { get; set; }
        [Index(18)] public int? BuildCount { get; set; }
        [Index(19)] public int? BuildMultiTargetingCount { get; set; }
        [Index(20)] public int? ContentFileCount { get; set; }
        [Index(21)] public int? RuntimeTargetCount { get; set; }
        [Index(22)] public int? ToolAssemblyCount { get; set; }
        [Index(23)] public int? EmbedAssemblyCount { get; set; }

        [Index(24)] public string ErrorBlobPath { get; set; }

        [Index(25)] public string RestoreLogMessageCodes { get; set; }
        [Index(26)] public bool? OnlyNU1202 { get; set; }
        [Index(27)] public bool? OnlyNU1213 { get; set; }
        [Index(28)] public string BuildErrorCodes { get; set; }
        [Index(29)] public bool? OnlyMSB3644 { get; set; }
    }
}
