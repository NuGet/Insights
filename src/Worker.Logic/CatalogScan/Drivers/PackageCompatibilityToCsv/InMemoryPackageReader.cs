// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;

namespace NuGet.Insights.Worker.PackageCompatibilityToCsv
{
    public class InMemoryPackageReader : PackageReaderBase
    {
        private readonly Memory<byte> _manifestBytes;
        private readonly IReadOnlyList<string> _files;

        public InMemoryPackageReader(Memory<byte> manifestBytes, IReadOnlyList<string> files) : base(DefaultFrameworkNameProvider.Instance)
        {
            _manifestBytes = manifestBytes;
            _files = files;
        }

        public override bool CanVerifySignedPackages(SignedPackageVerifierSettings verifierSettings)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<string> CopyFiles(string destination, IEnumerable<string> packageFiles, ExtractPackageFileDelegate extractFile, ILogger logger, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<byte[]> GetArchiveHashAsync(HashAlgorithmName hashAlgorithm, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override string GetContentHash(CancellationToken token, Func<string> GetUnsignedPackageHash = null)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<string> GetFiles()
        {
            return _files;
        }

        public override IEnumerable<string> GetFiles(string folder)
        {
            return _files.Where(x => x.StartsWith(folder + "/", StringComparison.Ordinal));
        }

        public override Task<PrimarySignature> GetPrimarySignatureAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Stream GetStream(string path)
        {
            if (path.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            {
                return _manifestBytes.AsStream();
            }

            throw new NotImplementedException();
        }

        public override Task<bool> IsSignedAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task ValidateIntegrityAsync(SignatureContent signatureContent, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            throw new NotImplementedException();
        }
    }
}
