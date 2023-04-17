// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.KustoIngestion
{
    public enum KustoIngestionState
    {
        Created,
        Expanding,
        Retrying,
        Enqueuing,
        Working,
        Validating,
        FailedValidation,
        SwappingTables,
        DroppingOldTables,
        Finalizing,
        Aborted,
        Complete,
    }

    public static class KustoIngestionStateExtensions
    {
        public static bool IsTerminal(this KustoIngestionState state)
        {
            return state == KustoIngestionState.FailedValidation
                || state == KustoIngestionState.Aborted
                || state == KustoIngestionState.Complete;
        }
    }
}
