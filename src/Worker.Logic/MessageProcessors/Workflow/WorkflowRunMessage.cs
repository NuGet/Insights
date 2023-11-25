// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace NuGet.Insights.Worker.Workflow
{
    public class WorkflowRunMessage
    {
        [JsonPropertyName("i")]
        public string RunId { get; set; }

        [JsonPropertyName("ac")]
        public int AttemptCount { get; set; }
    }
}
