// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public static class X509ExtensionInfoFactory
    {
        public static X509ExtensionInfo Create(X509Extension extension)
        {
            return extension switch
            {
                X509KeyUsageExtension ku => new X509KeyUsageExtensionInfo(ku),
                X509SubjectKeyIdentifierExtension ski => new X509SubjectKeyIdentifierExtensionInfo(ski),
                X509BasicConstraintsExtension bc => new X509BasicConstraintsExtensionInfo(bc),
                X509EnhancedKeyUsageExtension eku => new X509EnhancedKeyUsageExtensionInfo(eku),
                _ => new X509ExtensionInfo(extension, recognized: false),
            };
        }
    }
}
