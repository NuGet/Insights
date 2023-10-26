// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Data.Tables;

#nullable enable

namespace NuGet.Insights.StorageNoOpRetry
{
    public interface ITableEntityWithClientRequestId : ITableEntity
    {
        /// <summary>
        /// The client request ID used to last update the entity. This is used for no-oping some failed retries and is
        /// used for the x-ms-client-request-id header.
        /// </summary>
        public Guid? ClientRequestId { get; set; }
    }
}
