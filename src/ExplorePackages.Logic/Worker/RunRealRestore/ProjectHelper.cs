using Microsoft.Extensions.Logging;
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

        public string GetDotnetVersion()
        {
            var result = ExecuteDotnet("--version");
            return result.Output.Trim();
        }

        public string ClearAndCreateProject(string projectDir, ProjectProfile projectProfile)
        {
            if (Directory.Exists(projectDir))
            {
                Directory.Delete(projectDir, recursive: true);
            }
            Directory.CreateDirectory(projectDir);

            var newResult = CreateProject(projectDir, projectProfile);
            if (newResult.Output.Contains("Couldn't find an installed template that matches the input, searching online for one that does..."))
            {
                ExecuteDotnet("new", "-i", $"{projectProfile.TemplatePackageId}::{projectProfile.TemplatePackageVersion.ToNormalizedString()}");
                CreateProject(projectDir, projectProfile);
            }

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
            return ExecuteDotnet("new", projectProfile.TemplateName, "-o", projectDir, "-n", "TestProject");
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
            ExecuteDotnet("add", projectPath, "package", identity.Id, "-v", identity.Version.ToNormalizedString(), "-n");
        }

        public void Restore(string projectPath)
        {
            ExecuteDotnet("restore", projectPath);
        }

        public void Build(string projectPath)
        {
            ExecuteDotnet("build", projectPath);
        }

        public LockFile ReadAssetsFile(string projectPath)
        {
            var projectDir = Path.GetDirectoryName(projectPath);
            var assetsFilePath = Path.Combine(projectDir, "obj", LockFileFormat.AssetsFileName);
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

        private CommandResult ExecuteDotnet(params string[] arguments)
        {
            using var process = new Process();

            process.StartInfo.EnvironmentVariables.Add("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH", "false");
            // process.StartInfo.EnvironmentVariables.Add("DOTNET_CLI_TELEMETRY_OPTOUT", "true");
            // process.StartInfo.EnvironmentVariables.Add("DOTNET_GENERATE_ASPNET_CERTIFICATE", "false");
            // process.StartInfo.EnvironmentVariables.Add("DOTNET_NOLOGO", "true");

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

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var exited = process.WaitForExit(milliseconds: 20 * 1000);
            if (!exited)
            {
                process.Kill();
                throw new InvalidOperationException("The command took too long to complete.");  
            }

            _logger.LogInformation("Exit code: {ExitCode}", process.ExitCode);

            var output = string.Join(Environment.NewLine, outputQueue);
            _logger.LogInformation("Output:" + Environment.NewLine + "{Output}", output);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"The command failed with exit code {process.ExitCode}." + Environment.NewLine + output);
            }

            return new CommandResult(process.StartInfo.FileName, process.StartInfo.ArgumentList.ToList(), process.ExitCode, output);
        }

        private class CommandResult
        {
            public CommandResult(string fileName, IReadOnlyList<string> arguments, int exitCode, string output)
            {
                FileName = fileName;
                Arguments = arguments;
                ExitCode = exitCode;
                Output = output;
            }

            public string FileName { get; }
            public IReadOnlyList<string> Arguments { get; }
            public int ExitCode { get; }
            public string Output { get; }
        }
    }
}
