// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    [Flags]
    public enum CertificateRelationshipTypes
    {
        None = 0 << 0,
        PrimarySignedCmsContains = 1 << 0,
        AuthorTimestampSignedCmsContains = 1 << 1,
        RepositoryTimestampSignedCmsContains = 1 << 2,
        IsAuthorCodeSignedBy = 1 << 3,
        IsAuthorTimestampedBy = 1 << 4,
        IsRepositoryCodeSignedBy = 1 << 5,
        IsRepositoryTimestampedBy = 1 << 6,
        AuthorCodeSigningChainContains = 1 << 7,
        AuthorTimestampingChainContains = 1 << 8,
        RepositoryCodeSigningChainContains = 1 << 9,
        RepositoryTimestampingChainContains = 1 << 10,
    }
}
