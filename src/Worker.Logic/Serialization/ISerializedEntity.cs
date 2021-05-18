// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker
{
    public interface ISerializedEntity
    {
        JToken AsJToken();
        string AsString();
    }
}
