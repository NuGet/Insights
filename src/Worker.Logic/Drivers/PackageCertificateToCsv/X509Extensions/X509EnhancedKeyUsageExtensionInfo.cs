// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class X509EnhancedKeyUsageExtensionInfo : X509ExtensionInfo
    {
        public X509EnhancedKeyUsageExtensionInfo(X509EnhancedKeyUsageExtension extension) : base(extension, recognized: true)
        {
            EnhancedKeyUsageOids = extension
                .EnhancedKeyUsages
                .Cast<Oid>()
                .Select(x => x.Value)
                .ToList();
        }

        public List<string> EnhancedKeyUsageOids { get; }
    }
}
