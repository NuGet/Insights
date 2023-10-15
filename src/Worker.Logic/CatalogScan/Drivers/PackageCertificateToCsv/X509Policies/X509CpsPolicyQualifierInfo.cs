// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class X509CpsPolicyQualifierInfo : X509PolicyQualifierInfo
    {
        public X509CpsPolicyQualifierInfo(string policyQualifierId, string qualifier, string cpsUri) : base(policyQualifierId, qualifier, recognized: true)
        {
            CpsUri = cpsUri;
        }

        public string CpsUri { get; }
    }
}
