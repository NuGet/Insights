// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class StorageLeaseException : Exception
    {
        public StorageLeaseException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public StorageLeaseException(string message) : base(message)
        {
        }
    }
}
