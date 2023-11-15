// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageContentToCsv
{
    public class PackageContentToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageContentToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            FailFastLogLevel = LogLevel.None;
            AssertLogLevel = LogLevel.None;
        }

        public ICatalogLeafToCsvDriver<PackageContent> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageContent>>();

        [Fact]
        public async Task SimplePackage()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.13.13.44.11/microsoft.codecoverage.16.11.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Equal(3, records.Count);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.AllLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("LICENSE_NET.txt", records[0].Path);
            Assert.Equal("UR1YGf71sJ1pd83Xk5n/nCKd68is1NycbNhJ8QtWNTY=", records[0].SHA256);
            Assert.Equal(3, records[0].SequenceNumber);
            Assert.Equal(9126, records[0].Size);
            Assert.False(records[0].Truncated);
            Assert.Null(records[0].TruncatedSize);
            Assert.False(records[0].DuplicateContent);

            Assert.Equal("ThirdPartyNotices.txt", records[1].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[1].SHA256);
            Assert.Equal(4, records[1].SequenceNumber);
            Assert.Equal(1813, records[1].Size);
            Assert.False(records[1].Truncated);
            Assert.Null(records[1].TruncatedSize);
            Assert.False(records[1].DuplicateContent);

            Assert.Equal("build/netstandard1.0/ThirdPartyNotices.txt", records[2].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[2].SHA256);
            Assert.Equal(5, records[2].SequenceNumber);
            Assert.Equal(1813, records[2].Size);
            Assert.False(records[2].Truncated);
            Assert.Null(records[2].TruncatedSize);
            Assert.True(records[2].DuplicateContent);
        }

        [Fact]
        public async Task ObservesMaxPerFile()
        {
            ConfigureWorkerSettings = x => x.PackageContentMaxSizePerFile = 2000;

            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.13.13.44.11/microsoft.codecoverage.16.11.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Equal(3, records.Count);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.PartiallyLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("LICENSE_NET.txt", records[0].Path);
            Assert.Equal("UR1YGf71sJ1pd83Xk5n/nCKd68is1NycbNhJ8QtWNTY=", records[0].SHA256);
            Assert.Equal(3, records[0].SequenceNumber);
            Assert.Equal(9126, records[0].Size);
            Assert.True(records[0].Truncated);
            Assert.Equal(2000, records[0].TruncatedSize);
            Assert.Equal(2000, Encoding.UTF8.GetBytes(records[0].Content).Length);
            Assert.Equal(1986, records[0].Content.Length); // UTF-8 characters
            Assert.False(records[0].DuplicateContent);

            Assert.Equal("ThirdPartyNotices.txt", records[1].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[1].SHA256);
            Assert.Equal(4, records[1].SequenceNumber);
            Assert.Equal(1813, records[1].Size);
            Assert.False(records[1].Truncated);
            Assert.Null(records[1].TruncatedSize);
            Assert.False(records[1].DuplicateContent);

            Assert.Equal("build/netstandard1.0/ThirdPartyNotices.txt", records[2].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[2].SHA256);
            Assert.Equal(5, records[2].SequenceNumber);
            Assert.Equal(1813, records[2].Size);
            Assert.False(records[2].Truncated);
            Assert.Null(records[2].TruncatedSize);
            Assert.True(records[2].DuplicateContent);
        }

        [Theory]
        [InlineData(925, 925, 925, "Code. ")]
        [InlineData(926, 928, 926, "Code. \uFFFD")]
        [InlineData(927, 928, 926, "Code. \uFFFD")]
        [InlineData(928, 928, 926, "Code. “")]
        [InlineData(929, 929, 927, "Code. “D")]
        [InlineData(1097, 1097, 1093, "Distribute.\r")]
        [InlineData(1098, 1098, 1094, "Distribute.\r\n")]
        [InlineData(1099, 1101, 1095, "Distribute.\r\n\uFFFD")]
        [InlineData(1100, 1100, 1095, "Distribute.\r\n·")]
        [InlineData(1101, 1101, 1096, "Distribute.\r\n· ")]
        public async Task SplitsMultiByteCharacter(int limit, int contentBytes, int contentLength, string contentEndsWith)
        {
            ConfigureWorkerSettings = x => x.PackageContentMaxSizePerPackage = limit;

            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.13.13.44.11/microsoft.codecoverage.16.11.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Equal(3, records.Count);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.PartiallyLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("LICENSE_NET.txt", records[0].Path);
            Assert.Equal("UR1YGf71sJ1pd83Xk5n/nCKd68is1NycbNhJ8QtWNTY=", records[0].SHA256);
            Assert.Equal(3, records[0].SequenceNumber);
            Assert.Equal(9126, records[0].Size);
            Assert.True(records[0].Truncated);
            Assert.Equal(limit, records[0].TruncatedSize);
            Assert.EndsWith(contentEndsWith, records[0].Content, StringComparison.Ordinal);
            Assert.Equal(contentBytes, Encoding.UTF8.GetBytes(records[0].Content).Length);
            Assert.Equal(contentLength, records[0].Content.Length); // UTF-8 characters
            Assert.False(records[0].DuplicateContent);

            Assert.Equal("ThirdPartyNotices.txt", records[1].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[1].SHA256);
            Assert.Equal(4, records[1].SequenceNumber);
            Assert.Equal(1813, records[1].Size);
            Assert.True(records[1].Truncated);
            Assert.Equal(0, records[1].TruncatedSize);
            Assert.False(records[1].DuplicateContent);

            Assert.Equal("build/netstandard1.0/ThirdPartyNotices.txt", records[2].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[2].SHA256);
            Assert.Equal(5, records[2].SequenceNumber);
            Assert.Equal(1813, records[2].Size);
            Assert.True(records[2].Truncated);
            Assert.Equal(0, records[2].TruncatedSize);
            Assert.True(records[2].DuplicateContent);
        }

        [Fact]
        public async Task HandlesLastByteAsMultiByteCharacter()
        {
            ConfigureWorkerSettings = x => x.PackageContentMaxSizePerPackage = 926;

            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.13.13.44.11/microsoft.codecoverage.16.11.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Equal(3, records.Count);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.PartiallyLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("LICENSE_NET.txt", records[0].Path);
            Assert.Equal("UR1YGf71sJ1pd83Xk5n/nCKd68is1NycbNhJ8QtWNTY=", records[0].SHA256);
            Assert.Equal(3, records[0].SequenceNumber);
            Assert.Equal(9126, records[0].Size);
            Assert.True(records[0].Truncated);
            Assert.Equal(926, records[0].TruncatedSize);
            // See https://github.com/dotnet/docs/issues/38262 (behavior change in .NET 8)
            Assert.Equal(928, Encoding.UTF8.GetBytes(records[0].Content).Length); // has a Unicode replacement character
            Assert.Equal(926, records[0].Content.Length); // UTF-8 characters
            Assert.EndsWith("Code. \uFFFD", records[0].Content, StringComparison.Ordinal);
            Assert.False(records[0].DuplicateContent);

            Assert.Equal("ThirdPartyNotices.txt", records[1].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[1].SHA256);
            Assert.Equal(4, records[1].SequenceNumber);
            Assert.Equal(1813, records[1].Size);
            Assert.True(records[1].Truncated);
            Assert.Equal(0, records[1].TruncatedSize);
            Assert.False(records[1].DuplicateContent);

            Assert.Equal("build/netstandard1.0/ThirdPartyNotices.txt", records[2].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[2].SHA256);
            Assert.Equal(5, records[2].SequenceNumber);
            Assert.Equal(1813, records[2].Size);
            Assert.True(records[2].Truncated);
            Assert.Equal(0, records[2].TruncatedSize);
            Assert.True(records[2].DuplicateContent);
        }

        [Fact]
        public async Task ExceedsLimitOnFirstDuplicate()
        {
            ConfigureWorkerSettings = x => x.PackageContentMaxSizePerPackage = 9205;

            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.13.13.44.11/microsoft.codecoverage.16.11.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Equal(3, records.Count);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.PartiallyLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("LICENSE_NET.txt", records[0].Path);
            Assert.Equal("UR1YGf71sJ1pd83Xk5n/nCKd68is1NycbNhJ8QtWNTY=", records[0].SHA256);
            Assert.Equal(3, records[0].SequenceNumber);
            Assert.Equal(9126, records[0].Size);
            Assert.False(records[0].Truncated);
            Assert.Null(records[0].TruncatedSize);
            Assert.Equal(9126, Encoding.UTF8.GetBytes(records[0].Content).Length);
            Assert.Equal(9090, records[0].Content.Length); // UTF-8 characters
            Assert.False(records[0].DuplicateContent);

            Assert.Equal("ThirdPartyNotices.txt", records[1].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[1].SHA256);
            Assert.Equal(4, records[1].SequenceNumber);
            Assert.Equal(1813, records[1].Size);
            Assert.True(records[1].Truncated);
            Assert.Equal(79, records[1].TruncatedSize);
            Assert.Equal("CodeCoverage\r\n\r\nTHIRD-PARTY SOFTWARE NOTICES AND INFORMATION\r\nDo Not Translate ", records[1].Content);
            Assert.False(records[1].DuplicateContent);

            Assert.Equal("build/netstandard1.0/ThirdPartyNotices.txt", records[2].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[2].SHA256);
            Assert.Equal(5, records[2].SequenceNumber);
            Assert.Equal(1813, records[2].Size);
            Assert.True(records[2].Truncated);
            Assert.Equal(0, records[2].TruncatedSize);
            Assert.Null(records[2].Content);
            Assert.True(records[2].DuplicateContent);
        }

        [Fact]
        public async Task ExceedsLimitOnSecondDuplicate()
        {
            ConfigureWorkerSettings = x => x.PackageContentMaxSizePerPackage = 10950;

            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.13.13.44.11/microsoft.codecoverage.16.11.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Equal(3, records.Count);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.PartiallyLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("LICENSE_NET.txt", records[0].Path);
            Assert.Equal("UR1YGf71sJ1pd83Xk5n/nCKd68is1NycbNhJ8QtWNTY=", records[0].SHA256);
            Assert.Equal(3, records[0].SequenceNumber);
            Assert.Equal(9126, records[0].Size);
            Assert.False(records[0].Truncated);
            Assert.Null(records[0].TruncatedSize);
            Assert.Equal(9126, Encoding.UTF8.GetBytes(records[0].Content).Length);
            Assert.Equal(9090, records[0].Content.Length); // UTF-8 characters
            Assert.False(records[0].DuplicateContent);

            Assert.Equal("ThirdPartyNotices.txt", records[1].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[1].SHA256);
            Assert.Equal(4, records[1].SequenceNumber);
            Assert.Equal(1813, records[1].Size);
            Assert.False(records[1].Truncated);
            Assert.Null(records[1].TruncatedSize);
            Assert.Equal(1813, records[1].Content.Length);
            Assert.False(records[1].DuplicateContent);

            Assert.Equal("build/netstandard1.0/ThirdPartyNotices.txt", records[2].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[2].SHA256);
            Assert.Equal(5, records[2].SequenceNumber);
            Assert.Equal(1813, records[2].Size);
            Assert.True(records[2].Truncated);
            Assert.Equal(11, records[2].TruncatedSize);
            Assert.Null(records[2].Content);
            Assert.True(records[2].DuplicateContent);
        }

        [Fact]
        public async Task MatchesFirstAndSecondFileSize()
        {
            ConfigureWorkerSettings = x => x.PackageContentMaxSizePerPackage = 10939;

            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.13.13.44.11/microsoft.codecoverage.16.11.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Equal(3, records.Count);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.PartiallyLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("LICENSE_NET.txt", records[0].Path);
            Assert.Equal("UR1YGf71sJ1pd83Xk5n/nCKd68is1NycbNhJ8QtWNTY=", records[0].SHA256);
            Assert.Equal(3, records[0].SequenceNumber);
            Assert.Equal(9126, records[0].Size);
            Assert.False(records[0].Truncated);
            Assert.Null(records[0].TruncatedSize);
            Assert.Equal(9126, Encoding.UTF8.GetBytes(records[0].Content).Length);
            Assert.Equal(9090, records[0].Content.Length); // UTF-8 characters
            Assert.False(records[0].DuplicateContent);

            Assert.Equal("ThirdPartyNotices.txt", records[1].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[1].SHA256);
            Assert.Equal(4, records[1].SequenceNumber);
            Assert.Equal(1813, records[1].Size);
            Assert.False(records[1].Truncated);
            Assert.Null(records[1].TruncatedSize);
            Assert.Equal(1813, records[1].Content.Length);
            Assert.False(records[1].DuplicateContent);

            Assert.Equal("build/netstandard1.0/ThirdPartyNotices.txt", records[2].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[2].SHA256);
            Assert.Equal(5, records[2].SequenceNumber);
            Assert.Equal(1813, records[2].Size);
            Assert.True(records[2].Truncated);
            Assert.Equal(0, records[2].TruncatedSize);
            Assert.Null(records[2].Content);
            Assert.True(records[2].DuplicateContent);
        }

        [Fact]
        public async Task MatchesFirstFileSize()
        {
            ConfigureWorkerSettings = x => x.PackageContentMaxSizePerPackage = 9126;

            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.13.13.44.11/microsoft.codecoverage.16.11.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Equal(3, records.Count);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.PartiallyLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("LICENSE_NET.txt", records[0].Path);
            Assert.Equal("UR1YGf71sJ1pd83Xk5n/nCKd68is1NycbNhJ8QtWNTY=", records[0].SHA256);
            Assert.Equal(3, records[0].SequenceNumber);
            Assert.Equal(9126, records[0].Size);
            Assert.False(records[0].Truncated);
            Assert.Null(records[0].TruncatedSize);
            Assert.Equal(9126, Encoding.UTF8.GetBytes(records[0].Content).Length);
            Assert.Equal(9090, records[0].Content.Length); // UTF-8 characters
            Assert.False(records[0].DuplicateContent);

            Assert.Equal("ThirdPartyNotices.txt", records[1].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[1].SHA256);
            Assert.Equal(4, records[1].SequenceNumber);
            Assert.Equal(1813, records[1].Size);
            Assert.True(records[1].Truncated);
            Assert.Equal(0, records[1].TruncatedSize);
            Assert.Null(records[1].Content);
            Assert.False(records[1].DuplicateContent);

            Assert.Equal("build/netstandard1.0/ThirdPartyNotices.txt", records[2].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[2].SHA256);
            Assert.Equal(5, records[2].SequenceNumber);
            Assert.Equal(1813, records[2].Size);
            Assert.True(records[2].Truncated);
            Assert.Equal(0, records[2].TruncatedSize);
            Assert.Null(records[2].Content);
            Assert.True(records[2].DuplicateContent);
        }

        [Fact]
        public async Task ReadsOneByteFromFirstFile()
        {
            ConfigureWorkerSettings = x => x.PackageContentMaxSizePerPackage = 1;

            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.13.13.44.11/microsoft.codecoverage.16.11.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Equal(3, records.Count);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.PartiallyLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("LICENSE_NET.txt", records[0].Path);
            Assert.Equal("UR1YGf71sJ1pd83Xk5n/nCKd68is1NycbNhJ8QtWNTY=", records[0].SHA256);
            Assert.Equal(3, records[0].SequenceNumber);
            Assert.Equal(9126, records[0].Size);
            Assert.True(records[0].Truncated);
            Assert.Equal(1, records[0].TruncatedSize);
            Assert.Equal("M", records[0].Content);
            Assert.False(records[0].DuplicateContent);

            Assert.Equal("ThirdPartyNotices.txt", records[1].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[1].SHA256);
            Assert.Equal(4, records[1].SequenceNumber);
            Assert.Equal(1813, records[1].Size);
            Assert.True(records[1].Truncated);
            Assert.Equal(0, records[1].TruncatedSize);
            Assert.Null(records[1].Content);
            Assert.False(records[1].DuplicateContent);

            Assert.Equal("build/netstandard1.0/ThirdPartyNotices.txt", records[2].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[2].SHA256);
            Assert.Equal(5, records[2].SequenceNumber);
            Assert.Equal(1813, records[2].Size);
            Assert.True(records[2].Truncated);
            Assert.Equal(0, records[2].TruncatedSize);
            Assert.Null(records[2].Content);
            Assert.True(records[2].DuplicateContent);
        }

        [Fact]
        public async Task ReadsOneByteFromSecondFile()
        {
            ConfigureWorkerSettings = x => x.PackageContentMaxSizePerPackage = 9127;

            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.13.13.44.11/microsoft.codecoverage.16.11.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.CodeCoverage",
                PackageVersion = "16.11.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Equal(3, records.Count);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.PartiallyLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("LICENSE_NET.txt", records[0].Path);
            Assert.Equal("UR1YGf71sJ1pd83Xk5n/nCKd68is1NycbNhJ8QtWNTY=", records[0].SHA256);
            Assert.Equal(3, records[0].SequenceNumber);
            Assert.Equal(9126, records[0].Size);
            Assert.False(records[0].Truncated);
            Assert.Null(records[0].TruncatedSize);
            Assert.Equal(9126, Encoding.UTF8.GetBytes(records[0].Content).Length);
            Assert.Equal(9090, records[0].Content.Length); // UTF-8 characters
            Assert.False(records[0].DuplicateContent);

            Assert.Equal("ThirdPartyNotices.txt", records[1].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[1].SHA256);
            Assert.Equal(4, records[1].SequenceNumber);
            Assert.Equal(1813, records[1].Size);
            Assert.True(records[1].Truncated);
            Assert.Equal(1, records[1].TruncatedSize);
            Assert.Equal("C", records[1].Content);
            Assert.False(records[1].DuplicateContent);

            Assert.Equal("build/netstandard1.0/ThirdPartyNotices.txt", records[2].Path);
            Assert.Equal("T8QkSTW741FhZEAsVz8FOl0kZXHtTTV+bMzY6+wpa7w=", records[2].SHA256);
            Assert.Equal(5, records[2].SequenceNumber);
            Assert.Equal(1813, records[2].Size);
            Assert.True(records[2].Truncated);
            Assert.Equal(0, records[2].TruncatedSize);
            Assert.Null(records[2].Content);
            Assert.True(records[2].DuplicateContent);
        }

        [Fact]
        public async Task TreatsExtensionAsCaseInsensitive()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2022.02.27.10.17.20/fluentftp.37.0.2.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "FluentFTP",
                PackageVersion = "37.0.2",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Single(records);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.AllLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("LICENSE.TXT", records[0].Path);
            Assert.Equal("u5YfvjZlnoobtXafyAQbUE5OzqLeRFttLjh1B31af7w=", records[0].SHA256);
            Assert.Equal(21, records[0].SequenceNumber);
            Assert.Equal(1092, records[0].Size);
            Assert.False(records[0].Truncated);
            Assert.Null(records[0].TruncatedSize);
            Assert.Equal(1092, records[0].Content.Length);
            Assert.False(records[0].DuplicateContent);
        }

        [Fact]
        public async Task HandlesSuffixWithoutDots()
        {
            ConfigureWorkerSettings = x => x.PackageContentFileExtensions = new List<string> { "cense" };

            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.04.05.02.02.06/castle.core.4.4.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Castle.Core",
                PackageVersion = "4.4.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Single(records);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.AllLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal("cense", r.FileExtension));

            Assert.Equal("LICENSE", records[0].Path);
            Assert.Equal("rzo9KOX9c7H0UYS7rAX3ecWQfu/pCjwDJQcs1ixyaLQ=", records[0].SHA256);
            Assert.Equal(13, records[0].SequenceNumber);
            Assert.Equal(592, records[0].Size);
            Assert.False(records[0].Truncated);
            Assert.Null(records[0].TruncatedSize);
            Assert.Equal(592, records[0].Content.Length);
            Assert.False(records[0].DuplicateContent);
        }

        [Fact]
        public async Task HandlesHyphenInFrameworkProfile()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.16.02.24.43/nerdbank.gitversioning.1.3.13.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Nerdbank.GitVersioning",
                PackageVersion = "1.3.13",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var records = output.Value.Records;
            Assert.Single(records);
            Assert.All(records, r => Assert.Equal(PackageContentResultType.AllLoaded, r.ResultType));
            Assert.All(records, r => Assert.Equal(".txt", r.FileExtension));

            Assert.Equal("readme.txt", records[0].Path);
            Assert.Equal("/3zwnBsVeekSXSZKINsXd31xn+AcER6vLUDlx75toDw=", records[0].SHA256);
            Assert.Equal(12, records[0].SequenceNumber);
            Assert.Equal(11616, records[0].Size);
            Assert.False(records[0].Truncated);
            Assert.Null(records[0].TruncatedSize);
            Assert.Equal(11613, records[0].Content.Length);
            Assert.False(records[0].DuplicateContent);
        }
    }
}
