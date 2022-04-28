// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class ChainInfo
    {
        public ChainInfo(List<(string Fingerprint, X509Certificate2 Certificate)> certificates)
        {
            Certificates = certificates;
        }

        public List<(string Fingerprint, X509Certificate2 Certificate)> Certificates { get; }
    }
}
