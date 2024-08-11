// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging;

namespace NuGet.Insights.Worker.PackageManifestToCsv
{
    public class PackageManifestToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageManifestToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        public ICatalogLeafToCsvDriver<PackageManifestRecord> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageManifestRecord>>();

        [Fact]
        public void AllPublicInstanceMembersOnNuspecReaderAreAccountedFor()
        {
            var type = typeof(NuspecReader);
            var names = type.GetMembers(BindingFlags.Instance | BindingFlags.Public).Select(x => x.Name);

            var ignored = new HashSet<string>
            {
                nameof(NuspecReader.GetFrameworkReferenceGroups), // Deprecated, renamed
                nameof(NuspecReader.GetIdentity), // ID and version are stored separately in the record

                nameof(NuspecReader.GetMetadata), // Gives too raw of information
                nameof(NuspecReader.GetMetadataValue), // Gives too raw of information
                "get_" + nameof(NuspecReader.Xml), // Gives too raw of information
                nameof(NuspecReader.Xml), // Gives too raw of information
                
                ".ctor", // constructor
                nameof(Equals), // From base object
                nameof(GetHashCode), // From base object
                nameof(GetType), // From base object
                nameof(ToString), // From base object
            };

            var serialized = new Dictionary<string, string>
            {
                { nameof(NuspecReader.GetAuthors), nameof(PackageManifestRecord.Authors) },
                { nameof(NuspecReader.GetContentFiles), nameof(PackageManifestRecord.ContentFiles) },
                { nameof(NuspecReader.GetCopyright), nameof(PackageManifestRecord.Copyright) },
                { nameof(NuspecReader.GetDependencyGroups), nameof(PackageManifestRecord.DependencyGroups) },
                { nameof(NuspecReader.GetDescription), nameof(PackageManifestRecord.Description) },
                { nameof(NuspecReader.GetDevelopmentDependency), nameof(PackageManifestRecord.DevelopmentDependency) },
                { nameof(NuspecReader.GetFrameworkAssemblyGroups), nameof(PackageManifestRecord.FrameworkAssemblyGroups) },
                { nameof(NuspecReader.GetFrameworkReferenceGroups), nameof(PackageManifestRecord.FrameworkRefGroups) },
                { nameof(NuspecReader.GetFrameworkRefGroups), nameof(PackageManifestRecord.FrameworkRefGroups) },
                { nameof(NuspecReader.GetIcon), nameof(PackageManifestRecord.Icon) },
                { nameof(NuspecReader.GetIconUrl), nameof(PackageManifestRecord.IconUrl) },
                { nameof(NuspecReader.GetId), nameof(PackageManifestRecord.OriginalId) },
                { nameof(NuspecReader.GetLanguage), nameof(PackageManifestRecord.Language) },
                { nameof(NuspecReader.GetLicenseMetadata), nameof(PackageManifestRecord.LicenseMetadata) },
                { nameof(NuspecReader.GetLicenseUrl), nameof(PackageManifestRecord.LicenseUrl) },
                { nameof(NuspecReader.GetMinClientVersion), nameof(PackageManifestRecord.MinClientVersion) },
                { nameof(NuspecReader.GetOwners), nameof(PackageManifestRecord.Owners) },
                { nameof(NuspecReader.GetPackageTypes), nameof(PackageManifestRecord.PackageTypes) },
                { nameof(NuspecReader.GetProjectUrl), nameof(PackageManifestRecord.ProjectUrl) },
                { nameof(NuspecReader.GetReadme), nameof(PackageManifestRecord.Readme) },
                { nameof(NuspecReader.GetReferenceGroups), nameof(PackageManifestRecord.ReferenceGroups) },
                { nameof(NuspecReader.GetReleaseNotes), nameof(PackageManifestRecord.ReleaseNotes) },
                { nameof(NuspecReader.GetRepositoryMetadata), nameof(PackageManifestRecord.RepositoryMetadata) },
                { nameof(NuspecReader.GetRequireLicenseAcceptance), nameof(PackageManifestRecord.RequireLicenseAcceptance) },
                { nameof(NuspecReader.GetSummary), nameof(PackageManifestRecord.Summary) },
                { nameof(NuspecReader.GetTags), nameof(PackageManifestRecord.Tags) },
                { nameof(NuspecReader.GetTitle), nameof(PackageManifestRecord.Title) },
                { nameof(NuspecReader.GetVersion), nameof(PackageManifestRecord.OriginalVersion) },
                { nameof(NuspecReader.IsServiceable), nameof(PackageManifestRecord.IsServiceable) },
            };

            Assert.Empty(ignored.Except(names));
            Assert.Empty(serialized.Keys.Except(names));
            Assert.Empty(names.Except(ignored).Except(serialized.Keys));
        }

        [Fact]
        public async Task HandlesFormatExceptionFromContentFiles()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.10.31.20.06.37/apploggersenner007.1.1.8.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "AppLoggerSenner007",
                PackageVersion = "1.1.8",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageManifestRecordResultType.Error, record.ResultType);
            Assert.Null(record.ContentFiles);
            Assert.True(record.ContentFilesHasFormatException);
        }

        [Fact]
        public async Task HandlesMissingIdFromDependencyGroups()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.02.00.39.04/baseline.0.5.0.3.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Baseline",
                PackageVersion = "0.5.0.3",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageManifestRecordResultType.Error, record.ResultType);
            Assert.Null(record.DependencyGroups);
            Assert.True(record.DependencyGroupsHasMissingId);
        }

        [Fact]
        public async Task SerializesPackageTypes()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.02.20.19.21.13/dotnet-reportgenerator-globaltool.4.8.6.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "dotnet-reportgenerator-globaltool",
                PackageVersion = "4.8.6",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageManifestRecordResultType.Available, record.ResultType);
            Assert.Equal(
                JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        Name = "DotnetTool",
                        Version = "0.0",
                    }
                }),
                record.PackageTypes);
        }

        [Fact]
        public async Task SerializesContentFiles()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.11.29.14.49.51/contentfilesexample.1.0.2.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "ContentFilesExample",
                PackageVersion = "1.0.2",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageManifestRecordResultType.Available, record.ResultType);
            Assert.Equal(
                JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        BuildAction = "Content",
                        CopyToOutput = (bool?)null,
                        Flatten = (bool?)null,
                        Include = "**/images/*.*",
                    },
                    new
                    {
                        BuildAction = "Content",
                        CopyToOutput = (bool?)null,
                        Flatten = (bool?)null,
                        Include = "**/data.txt",
                    },
                    new
                    {
                        BuildAction = "None",
                        CopyToOutput = (bool?)true,
                        Flatten = (bool?)false,
                        Include = "**/tools/*",
                    },
                }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
                record.ContentFiles);
        }

        [Fact]
        public async Task SerializesLicenseExpression()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.02.20.15.53.38/csvhelper.24.0.1.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "CsvHelper",
                PackageVersion = "24.0.1",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageManifestRecordResultType.Available, record.ResultType);
            Assert.Equal(
                JsonSerializer.Serialize(new
                {
                    License = "MS-PL OR Apache-2.0",
                    LicenseUrl = "https://licenses.nuget.org/MS-PL%20OR%20Apache-2.0",
                    Type = "Expression",
                    Version = "1.0.0",
                }),
                record.LicenseMetadata);
        }

        [Fact]
        public async Task SerializesDependencies()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.12.16.06.30.20/knapcode.torsharp.2.5.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Knapcode.TorSharp",
                PackageVersion = "2.5.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageManifestRecordResultType.Available, record.ResultType);
            Assert.Equal(JsonSerializer.Serialize(new[]
            {
                new
                {
                    Packages = new object[0],
                    TargetFramework = "net45",
                },
                new
                {
                    Packages = new object[]
                    {
                        new
                        {
                            Exclude = new[]
                            {
                                "Analyzers",
                                "Build"
                            },
                            Id = "System.ServiceModel.Syndication",
                            Include = new string[0],
                            VersionRange = "[4.5.0, )"
                        },
                        new
                        {
                            Exclude = new[]
                            {
                                "Analyzers",
                                "Build"
                            },
                            Id = "sharpcompress",
                            Include = new string[0],
                            VersionRange = "[0.24.0, )"
                      }
                    },
                    TargetFramework = "netstandard2.0",
                },
            }), record.DependencyGroups);
        }

        [Fact]
        public async Task SerializesFrameworkAssemblyGroups()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.12.16.06.30.20/knapcode.torsharp.2.5.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Knapcode.TorSharp",
                PackageVersion = "2.5.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageManifestRecordResultType.Available, record.ResultType);
            Assert.Equal(JsonSerializer.Serialize(new[]
            {
                new
                {
                    Items = new[]
                    {
                        "System.IO.Compression",
                        "System.Net.Http",
                        "System.ServiceModel",
                    },
                    TargetFramework = "net45",
                },
            }), record.FrameworkAssemblyGroups);
        }

        [Fact]
        public async Task SerializesReferenceGroups()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2015.07.22.19.22.26/bundletransformer.core.1.9.69.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "BundleTransformer.Core",
                PackageVersion = "1.9.69",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageManifestRecordResultType.Available, record.ResultType);
            Assert.Equal(JsonSerializer.Serialize(new[]
            {
                new
                {
                    Items = new[]
                    {
                        "BundleTransformer.Core.dll",
                    },
                    TargetFramework = "any",
                },
            }), record.ReferenceGroups);
        }

        [Fact]
        public async Task SerializesRepositoryMetadata()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.01.03.03.09.45/sleet.4.0.18.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Sleet",
                PackageVersion = "4.0.18",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageManifestRecordResultType.Available, record.ResultType);
            Assert.Equal(JsonSerializer.Serialize(new
            {
                Branch = "HEAD",
                Commit = "5d483588c1f3e3f09426ffad1b45020ec09ec1d5",
                Type = "git",
                Url = "https://github.com/emgarten/Sleet",
            }), record.RepositoryMetadata);
        }

        [Fact]
        public async Task SerializesFrameworkReferenceGroups()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.08.11.18.53.37/microsoft.toolkit.wpf.ui.controls.6.1.2.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.Toolkit.Wpf.UI.Controls",
                PackageVersion = "6.1.2",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageManifestRecordResultType.Available, record.ResultType);
            Assert.Equal(JsonSerializer.Serialize(new[]
            {
                new
                {
                    FrameworkReferences = new[]
                    {
                        new { Name = "Microsoft.WindowsDesktop.App.WPF" },
                    },
                    TargetFramework = "netcoreapp3.0",
                },
            }), record.FrameworkRefGroups);
        }
    }
}
