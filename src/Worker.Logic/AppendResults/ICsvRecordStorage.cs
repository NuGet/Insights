// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public interface ICsvRecordStorage
    {
        /// <summary>
        /// The Azure Blob Storage container name to write CSV results to.
        /// </summary>
        string ContainerName { get; }

        Type RecordType { get; }

        string BlobNamePrefix { get; }
    }
}
