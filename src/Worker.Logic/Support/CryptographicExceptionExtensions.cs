// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using NuGet.Packaging.Signing;

namespace NuGet.Insights.Worker
{
    public static class CryptographicExceptionExtensions
    {
        private static readonly string GetTimestampsMethodName = typeof(Signature).FullName + ".GetTimestamps";

        public static bool IsInvalidDataException(this CryptographicException ex)
        {
            if (!ex.StackTrace.Contains(GetTimestampsMethodName, StringComparison.Ordinal))
            {
                return false;
            }

            return ex.Message == "The ASN.1 data is invalid."
                || ex.Message == "Error occurred during a cryptographic operation.";
        }
    }
}
