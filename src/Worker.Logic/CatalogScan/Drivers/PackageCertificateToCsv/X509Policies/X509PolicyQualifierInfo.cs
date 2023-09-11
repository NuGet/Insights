// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    /// <summary>
    /// From RFC 5280 (https://www.rfc-editor.org/rfc/rfc5280#section-4.2.1.4):
    /// 
    ///     PolicyQualifierInfo ::= SEQUENCE {
    ///         policyQualifierId PolicyQualifierId,
    ///         qualifier          ANY DEFINED BY policyQualifierId }
    ///
    ///     -- policyQualifierIds for Internet policy qualifiers
    ///
    ///     id-qt          OBJECT IDENTIFIER::=  { id-pkix 2 }
    ///     id-qt-cps      OBJECT IDENTIFIER ::=  { id-qt 1 }
    ///     id-qt-unotice  OBJECT IDENTIFIER ::=  { id-qt 2 }
    ///
    ///     PolicyQualifierId ::= OBJECT IDENTIFIER ( id-qt-cps | id-qt-unotice )
    /// </summary>
    public class X509PolicyQualifierInfo
    {
        public X509PolicyQualifierInfo(string policyQualifierId, string qualifier)
        {
            PolicyQualifierId = policyQualifierId;
            Qualifier = qualifier;
        }

        public string PolicyQualifierId { get; }

        /// <summary>
        /// Base64 encoded bytes for the qualifier.
        /// </summary>
        public string Qualifier { get; }
    }
}
