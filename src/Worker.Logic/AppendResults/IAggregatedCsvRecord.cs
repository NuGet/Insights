// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public interface IAggregatedCsvRecord : ICsvRecord
    {
    }

    public interface IAggregatedCsvRecord<T> : IAggregatedCsvRecord, ICsvRecord<T> where T : IAggregatedCsvRecord<T>
    {
        /// <summary>
        /// Prune the provided records to remove duplicate or old data. Packages are unlisted, relisted, reflowed, or
        /// appear in the catalog again for some reason, this method is called to prune out duplicate CSV records.
        ///
        /// Use the <paramref name="isFinalPrune"/> parameter to choose between intermediate pruning logic and the final
        /// pruning logic. Intermediate pruning logic may leave marker deleted records for deduplicate purposes in the
        /// final pruning phases.
        /// </summary>
        /// <param name="records">The records to prune.</param>
        /// <param name="isFinalPrune">Whether or not this prune invocation is the last one prior to saving the CSV file.</param>
        /// <param name="options">The worker settings.</param>
        /// <param name="logger">The logger, provided by the call site.</param>
        /// <returns>The records, after pruning out undesired records.</returns>
        static abstract List<T> Prune(List<T> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger);

        /// <summary>
        /// The message schema name for the <see cref="CsvCompactMessage{T}"/> specific to this record type. Must start
        /// with "cc." and be unique, e.g. "cc.fb" for "FooBarRecord".
        /// </summary>
        static abstract string CsvCompactMessageSchemaName { get; }
    }
}
