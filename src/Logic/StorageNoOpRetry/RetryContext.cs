// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public class RetryContext
    {
        public RetryContext(Guid clientRequestId)
        {
            ClientRequestId = clientRequestId;
            ClientRequestIdString = clientRequestId.ToString();
        }

        internal RetryContext(RetryContext context)
        {
            ClientRequestId = context.ClientRequestId;
            ClientRequestIdString = context.ClientRequestIdString;
        }

        public Guid ClientRequestId { get; }
        public string ClientRequestIdString { get; }
        public int Attempts { get; set; }
    }
}
