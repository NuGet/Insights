using NuGet.ProjectModel;
using System;
using NuGetPackageIdentity = NuGet.Packaging.Core.PackageIdentity;

namespace Knapcode.ExplorePackages.Logic.Worker.RunRealRestore
{
    public class RealRestoreResult
    {
        public RealRestoreResult()
        {
        }

        public RealRestoreResult(DateTimeOffset timestamp, string dotnetVersion, NuGetPackageIdentity package, ProjectProfile projectProfile)
        {
            Timestamp = timestamp;
            DotnetVersion = dotnetVersion;
            Id = package.Id;
            Version = package.Version.ToNormalizedString();
            Framework = projectProfile.Framework.GetShortFolderName();
            Template = projectProfile.TemplateName;
        }

        public RealRestoreResult(
            DateTimeOffset timestamp,
            string dotnetVersion,
            NuGetPackageIdentity package,
            ProjectProfile projectProfile,
            LockFile assetsFile,
            LockFileTarget target,
            LockFileTargetLibrary library)
            : this(timestamp, dotnetVersion, package, projectProfile)
        {
            RestoreSucceeded = true;
            BuildSucceeded = true;

            TargetCount = assetsFile.Targets.Count;
            LibraryCount = target.Libraries.Count;

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

        public DateTimeOffset Timestamp { get; }
        public string DotnetVersion { get; }

        public string Id { get; set; }
        public string Version { get; set; }
        public string Framework { get; set; }
        public string Template { get; }

        public bool RestoreSucceeded { get; set; }
        public bool BuildSucceeded { get; set; }

        public int TargetCount { get; set; }
        public int LibraryCount { get; set; }

        public int DependencyCount { get; set; }
        public int FrameworkAssemblyCount { get; set; }
        public int FrameworkReferenceCount { get; set; }
        public int RuntimeAssemblyCount { get; set; }
        public int ResourceAssemblyCount { get; set; }
        public int CompileTimeAssemblyCount { get; set; }
        public int NativeLibraryCount { get; set; }
        public int BuildCount { get; set; }
        public int BuildMultiTargetingCount { get; set; }
        public int ContentFileCount { get; set; }
        public int RuntimeTargetCount { get; set; }
        public int ToolAssemblyCount { get; set; }
        public int EmbedAssemblyCount { get; set; }
    }
}
