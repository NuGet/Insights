// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;

namespace NuGet.Insights.Worker
{
    public interface ISchemaDeserializer
    {
        string Name { get; }
        Type Type { get; }
        object Deserialize(int schemaVersion, JsonElement data);
    }
}
