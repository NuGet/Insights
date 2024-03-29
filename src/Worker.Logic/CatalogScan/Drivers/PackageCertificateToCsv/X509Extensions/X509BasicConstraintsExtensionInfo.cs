// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class X509BasicConstraintsExtensionInfo : X509ExtensionInfo
    {
        public X509BasicConstraintsExtensionInfo(X509BasicConstraintsExtension extension) : base(extension, recognized: true)
        {
            CertificateAuthority = extension.CertificateAuthority;
            HasPathLengthConstraint = extension.HasPathLengthConstraint;
            PathLengthConstraint = extension.PathLengthConstraint;
        }

        public bool CertificateAuthority { get; }
        public bool HasPathLengthConstraint { get; }
        public int PathLengthConstraint { get; }
    }
}
