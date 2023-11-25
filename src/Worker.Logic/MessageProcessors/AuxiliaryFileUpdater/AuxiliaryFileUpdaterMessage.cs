// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public class AuxiliaryFileUpdaterMessage<T> : ITaskStateMessage
    {
        [JsonPropertyName("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonPropertyName("ac")]
        public int AttemptCount { get; set; }
    }
}
