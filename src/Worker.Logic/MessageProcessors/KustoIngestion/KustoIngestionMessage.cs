// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionMessage
    {
        public string IngestionId { get; set; }
        public int AttemptCount { get; set; }
    }
}
