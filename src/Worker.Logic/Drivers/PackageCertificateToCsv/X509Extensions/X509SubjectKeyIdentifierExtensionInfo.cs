// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class X509SubjectKeyIdentifierExtensionInfo : X509ExtensionInfo
    {
        public X509SubjectKeyIdentifierExtensionInfo(X509SubjectKeyIdentifierExtension extension) : base(extension, recognized: true)
        {
            SubjectKeyIdentifier = extension.SubjectKeyIdentifier;
        }

        public string SubjectKeyIdentifier { get; }
    }
}
