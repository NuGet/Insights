// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public interface ISchemaSerializer<T>
    {
        string Name { get; }
        int LatestVersion { get; }
        ISerializedEntity SerializeData(T message);
        ISerializedEntity SerializeMessage(T message);
    }

    public interface ISchemaSerializer
    {
        public string Name { get; }
        ISerializedEntity SerializeMessage(object message);
    }
}
