// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Services.Validation;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class PackageCertificateToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageCertificateToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            FailFastLogLevel = LogLevel.None;
            AssertLogLevel = LogLevel.None;
        }

        public ICatalogLeafToCsvBatchDriver<PackageCertificateRecord, CertificateRecord> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvBatchDriver<PackageCertificateRecord, CertificateRecord>>();

        [Fact]
        public async Task HandlesTimeNotValidCodeSigningCertificate()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2022.06.28.00.44.15/microsoft.extensions.primitives.5.0.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.Extensions.Primitives",
                PackageVersion = "5.0.0",
            };

            var output = await Target.ProcessLeavesAsync(new[] { leaf });

            var record = output
                .Result
                .Sets2
                .SelectMany(x => x.Records)
                .Single(x => x.Fingerprint == "P5AB6oPFYNcSwkzyE8PTEss7_1HuiUNdNDC9BrXQ7s4");
            Assert.Equal(X509ChainStatusFlags.NotTimeValid, record.CodeSigningStatusFlags);
            Assert.Equal(EndCertificateStatus.Invalid, record.CodeSigningStatus);
            Assert.NotNull(record.CodeSigningStatusUpdateTime);
        }

        [Fact]
        public async Task HandlesRevokedCodeSigningCertificate()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.01.30.01.58.32/purewebsockets.2.4.0-beta.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "PureWebSockets",
                PackageVersion = "2.4.0-beta",
            };

            var output = await Target.ProcessLeavesAsync(new[] { leaf });

            var record = output
                .Result
                .Sets2
                .SelectMany(x => x.Records)
                .Single(x => x.Fingerprint == "h9f-OjnpjgG78Lrw7n8NF3V6AxXhmRdVrhIhqjlCstg");
            Assert.Equal(X509ChainStatusFlags.NotTimeValid | X509ChainStatusFlags.Revoked, record.CodeSigningStatusFlags);
            Assert.Equal(EndCertificateStatus.Invalid, record.CodeSigningStatus);
            Assert.NotNull(record.CodeSigningStatusUpdateTime);
        }

        [Fact]
        public async Task HandlesTimeNotValidTimestampingCertificate()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2023.06.14.01.27.35/polly.7.2.4.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Polly",
                PackageVersion = "7.2.4",
            };

            var output = await Target.ProcessLeavesAsync(new[] { leaf });

            var record = output
                .Result
                .Sets2
                .SelectMany(x => x.Records)
                .Single(x => x.Fingerprint == "7_ZekD9usdyf5KCnVvV-2QoJeampaEDneSpymAkASlY");
            Assert.Equal(X509ChainStatusFlags.NotTimeValid, record.TimestampingStatusFlags);
            Assert.Equal(EndCertificateStatus.Invalid, record.TimestampingStatus);
            Assert.NotNull(record.TimestampingStatusUpdateTime);
        }

        [Fact]
        public async Task HandlesRevokedTimestampingCertificate()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.11.19.08.01.13/digi21.diging.io.shp.x64.17.0.1.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Digi21.DigiNG.Io.Shp.x64",
                PackageVersion = "17.0.1",
            };

            var output = await Target.ProcessLeavesAsync(new[] { leaf });

            var record = output
                .Result
                .Sets2
                .SelectMany(x => x.Records)
                .Single(x => x.Fingerprint == "67Fpkkmfe6SfXoiCJsZwHvDV6PmPL5kvhXHdFH7Dr9Q");
            Assert.Equal(X509ChainStatusFlags.Revoked, record.TimestampingStatusFlags);
            Assert.Equal(EndCertificateStatus.Revoked, record.TimestampingStatus);
            Assert.NotNull(record.TimestampingStatusUpdateTime);
        }

        [Fact]
        public async Task HandlesCorruptASN1InAuthorTimestamp()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.11.04.15.12.15/igniteui.mvc.4.20.1.15.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "IgniteUI.MVC.4",
                PackageVersion = "20.1.15",
            };

            var output = await Target.ProcessLeavesAsync(new[] { leaf });

            Assert.Empty(output.Result.Sets1
                .SelectMany(x => x.Records)
                .Where(x => x.RelationshipTypes.HasFlag(CertificateRelationshipTypes.IsAuthorTimestampedBy)));
            Assert.Single(output.Result.Sets1
                .SelectMany(x => x.Records)
                .Where(x => x.RelationshipTypes.HasFlag(CertificateRelationshipTypes.IsRepositoryTimestampedBy)));
        }

        [Fact]
        public async Task HandlesCorruptEncodingOfAuthorTimestamp()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2022.02.08.11.43.32/avira.managed.remediation.1.2202.701.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Avira.Managed.Remediation",
                PackageVersion = "1.2202.701",
            };

            var output = await Target.ProcessLeavesAsync(new[] { leaf });

            // .NET Runtime 6.0.0 fails to read this. 6.0.5 succeeds.
            Assert.Single(output.Result.Sets1
                .SelectMany(x => x.Records)
                .Where(x => x.RelationshipTypes.HasFlag(CertificateRelationshipTypes.IsAuthorTimestampedBy)));
            Assert.Single(output.Result.Sets1
                .SelectMany(x => x.Records)
                .Where(x => x.RelationshipTypes.HasFlag(CertificateRelationshipTypes.IsRepositoryTimestampedBy)));
        }

        [Fact]
        public async Task HandlesEVCodeSigning()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2022.01.25.12.39.12/codesign.0.0.1.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "CodeSign",
                PackageVersion = "0.0.1",
            };

            var output = await Target.ProcessLeavesAsync(new[] { leaf });

            var certificates = output.Result.Sets2.SelectMany(x => x.Records).ToList();
            Assert.NotEmpty(certificates);
            var certificate = Assert.Single(certificates, x => x.FingerprintSHA256Hex == "FB32E016FD317DB68C0B2B5B6E33231EE932B4B21E27F32B51654A483A10ADFB");
            Assert.NotNull(certificate.Policies);
            Assert.Contains("https://www.digicert.com/CPS", certificate.Policies);
            var genericPolicies = JsonConvert.DeserializeObject<List<X509PolicyInfo>>(certificate.Policies);
            Assert.Equal(2, genericPolicies.Count);
            Assert.Equal("2.16.840.1.114412.3.2", genericPolicies[0].PolicyIdentifier);
            Assert.Single(genericPolicies[0].PolicyQualifiers);
            Assert.Equal(Oids.IdQtCps.Value, genericPolicies[0].PolicyQualifiers[0].PolicyQualifierId);
            Assert.Equal("2.23.140.1.3", genericPolicies[1].PolicyIdentifier);
            Assert.Empty(genericPolicies[1].PolicyQualifiers);
        }

        [Fact]
        public async Task MissingAuthorTimestampingChainCertificate()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.03.29.20.48.16/awhewell.owin.webapi.1.0.0-beta37.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "AWhewell.Owin.WebApi",
                PackageVersion = "1.0.0-beta37",
            };

            var output = await Target.ProcessLeavesAsync(new[] { leaf });

            Assert.Empty(output.Failed);
            Assert.Empty(output.TryAgainLater);
            var pc = Assert.Single(output.Result.Sets1).Records;
            var justInChain = pc
                .Where(x => x.RelationshipTypes == CertificateRelationshipTypes.AuthorTimestampingChainContains)
                .OrderBy(x => x.Fingerprint)
                .ToList();
            Assert.InRange(justInChain.Count, 1, 2);
            var justInChainFingerprints = string.Join(' ', justInChain.Select(x => x.Fingerprint));

            // There are at least two valid chains for the author timestamping certificate. The built chain can be
            // either of these. Typically there is only a single chain because all of the certificates are included in
            // the signed CMS. But this package does not followg that rule.
            //
            // See here for a list of known certificates with the same issuer:
            // https://search.censys.io/authorities/2c1fb55882eb4d8c782a3fd3eb37e60c0518b5eedd91149a5b3e5a5a234a1c5f
            Assert.Contains(
                justInChainFingerprints,
                new[]
                {
                    "16eg-11-JzHXcelITrze9x1fDD4KKUh4K8g-4OppnvQ aLnHYSGaWx8BMXhEdGZdthu9sQngDwXKn3QkTuX19Ss",
                    // Intermediate: https://search.censys.io/certificates/68b9c761219a5b1f0131784474665db61bbdb109e00f05ca9f74244ee5f5f52b
                    // Root: https://search.censys.io/certificates/d7a7a0fb5d7e2731d771e9484ebcdef71d5f0c3e0a2948782bc83ee0ea699ef4

                    "55PJsC_YqhPiHDEiisywgRlkO3SciYlksXRtRsPUy9I",
                    // Root: https://search.censys.io/certificates/e793c9b02fd8aa13e21c31228accb08119643b749c898964b1746d46c3d4cbd2
                });
        }
    }
}
