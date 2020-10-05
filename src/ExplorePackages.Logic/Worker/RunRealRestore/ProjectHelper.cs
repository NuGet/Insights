using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using NuGetPackageIdentity = NuGet.Packaging.Core.PackageIdentity;

namespace Knapcode.ExplorePackages.Logic.Worker.RunRealRestore
{
    public class ProjectHelper
    {
        private readonly ILogger<ProjectHelper> _logger;

        public ProjectHelper(ILogger<ProjectHelper> logger)
        {
            _logger = logger;
        }

        public List<CommandResult> CommandResults { get; } = new List<CommandResult>();

        public string GetDotnetVersion()
        {
            return ExecuteDotnet(new[] { "--version" }).Output.Trim();
        }

        public string ClearAndCreateProject(string projectDir, ProjectProfile projectProfile)
        {
            if (Directory.Exists(projectDir))
            {
                Directory.Delete(projectDir, recursive: true);
            }
            Directory.CreateDirectory(projectDir);

            // Execute this with a machine-wide lock since it has showed to have concurrency issues.
            var lockPath = Path.Combine(Path.GetTempPath(), "ExplorePackages.Knapcode", "install-template-lock");
            ConcurrencyUtilities.ExecuteWithFileLocked(
                lockPath,
                () =>
                {
                    var newResult = CreateProject(projectDir, projectProfile);
                    if (newResult.Output.Contains("Couldn't find an installed template that matches the input, searching online for one that does..."))
                    {
                        ExecuteDotnet(new[] { "new", "-i", $"{projectProfile.TemplatePackage.Id}::{projectProfile.TemplatePackage.Version.ToNormalizedString()}" });
                        CreateProject(projectDir, projectProfile);
                    }
                });

            var projectPath = Path.Combine(projectDir, "TestProject.csproj");
            if (!File.Exists(projectPath))
            {
                var allFiles = Directory.EnumerateFileSystemEntries(Path.GetTempPath(), "*", SearchOption.AllDirectories);
                _logger.LogError("Temp directory:" + Environment.NewLine + "{Files}", string.Join(Environment.NewLine, allFiles));
                throw new InvalidOperationException($"The project should exist at path: {projectPath}");
            }

            return projectPath;
        }

        private CommandResult CreateProject(string projectDir, ProjectProfile projectProfile)
        {
            return ExecuteDotnet(new[] { "new", projectProfile.TemplateName, "-o", projectDir, "-n", "TestProject" });
        }

        public void SetFramework(string projectPath, NuGetFramework framework)
        {
            using var fileStream = new FileStream(projectPath, FileMode.Open, FileAccess.ReadWrite);
            var document = XDocument.Load(fileStream);
            var targetFramework = document.XPathSelectElement("/Project/PropertyGroup/TargetFramework");

            if (targetFramework == null)
            {
                throw new InvalidOperationException("Could not find the <TargetFramework> element in the project.");
            }

            targetFramework.Value = framework.GetShortFolderName();

            fileStream.Position = 0;
            fileStream.SetLength(0);
            document.Save(fileStream);
        }

        public void AddPackage(string projectPath, NuGetPackageIdentity identity)
        {
            ExecuteDotnet(new[] { "add", projectPath, "package", identity.Id, "-v", identity.Version.ToNormalizedString(), "-n" });
        }

        public CommandResult Restore(string projectPath)
        {
            return ExecuteDotnet(new[] { "restore", projectPath }, throwOnFailure: false);
        }

        public CommandResult Build(string projectPath)
        {
            return ExecuteDotnet(new[] { "build", projectPath }, throwOnFailure: false);
        }

        public LockFile ReadAssetsFileOrNull(string projectPath)
        {
            var projectDir = Path.GetDirectoryName(projectPath);
            var assetsFilePath = Path.Combine(projectDir, "obj", LockFileFormat.AssetsFileName);

            if (!File.Exists(assetsFilePath))
            {
                return null;
            }

            var format = new LockFileFormat();
            return format.Read(assetsFilePath);
        }

        public LockFileTarget GetMatchingTarget(LockFile assetsFile, NuGetFramework framework)
        {
            var targets = assetsFile
                .Targets
                .Where(x => x.TargetFramework == framework)
                .ToList();
            if (!targets.Any())
            {
                throw new InvalidOperationException($"No target matching framework '{framework}' was found in the assets file.");
            }

            return targets.First();
        }

        public LockFileTargetLibrary GetMatchingLibrary(LockFileTarget target, NuGetPackageIdentity package)
        {
            var libraries = target
                .Libraries
                .Where(x => x.Name.Equals(package.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (!libraries.Any())
            {
                throw new InvalidOperationException($"No library matching ID '{package.Id}' was found in the assets file.");
            }

            var library = libraries.First();
            if (library.Version != package.Version)
            {
                throw new InvalidOperationException($"The library matching ID '{package.Id}' has an unexpected version ('{library.Version}' instead of '{package.Version}').");
            }

            return library;
        }

        private CommandResult ExecuteDotnet(string[] arguments, bool throwOnFailure = true)
        {
            using var process = new Process();

            // If we don't disable this, the .NET CLI fails with unauthorized errors in the Win32 registry.
            process.StartInfo.EnvironmentVariables.Add("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH", "false");

            process.StartInfo.FileName = "dotnet";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            _logger.LogInformation("Command: {FileName} {Arguments}", process.StartInfo.FileName, arguments);

            var outputQueue = new ConcurrentQueue<string>();
            process.OutputDataReceived += (s, e) => outputQueue.Enqueue(e.Data);
            process.ErrorDataReceived += (s, e) => outputQueue.Enqueue(e.Data);

            var startTimestamp = DateTimeOffset.UtcNow;

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var exited = process.WaitForExit(milliseconds: (int)TimeSpan.FromMinutes(5).TotalMilliseconds);

            var timeout = false;
            if (!exited)
            {
                process.Kill();
                timeout = true;
            }
            else
            {
                // We must call this to flush all asynchronous events.
                process.WaitForExit();
            }

            var endTimestamp = DateTimeOffset.UtcNow;

            if (exited)
            {
                _logger.LogInformation("Exit code: {ExitCode}", process.ExitCode);
            }

            var output = string.Join(Environment.NewLine, outputQueue);
            _logger.LogInformation("Output:" + Environment.NewLine + "{Output}", output);

            var result = new CommandResult(
                startTimestamp,
                endTimestamp,
                process.StartInfo.FileName,
                process.StartInfo.ArgumentList.ToList(),
                timeout ? (int?)null : process.ExitCode,
                timeout,
                output);

            CommandResults.Add(result);

            if (timeout)
            {
                throw new InvalidOperationException("The command took too long to complete.");
            }

            if (throwOnFailure && process.ExitCode != 0)
            {
                throw new InvalidOperationException($"The command failed with exit code {process.ExitCode}." + Environment.NewLine + output);
            }

            return result;
        }
    }
}
