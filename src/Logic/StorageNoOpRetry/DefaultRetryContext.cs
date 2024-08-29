// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public sealed class DefaultRetryContext : RetryContext
    {
        public DefaultRetryContext(Guid clientRequestId) : base(clientRequestId)
        {
        }

        public DefaultRetryContext(RetryContext context) : base(context)
        {
        }
    }
}
