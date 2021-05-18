// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface IMessageBatcher
    {
        Task<IReadOnlyList<HomogeneousBatchMessage>> BatchOrNullAsync<T>(IReadOnlyList<T> messages, ISchemaSerializer<T> serializer);
    }
}