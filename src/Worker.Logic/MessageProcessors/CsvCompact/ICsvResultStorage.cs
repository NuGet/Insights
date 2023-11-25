// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public delegate List<T> Prune<T>(List<T> records, bool isFinalPrune) where T : ICsvRecord;

    public interface ICsvResultStorage<T> where T : ICsvRecord
    {
        /// <summary>
        /// The Azure Blob Storage container name to write CSV results to.
        /// </summary>
        string ResultContainerName { get; }

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
        /// <returns>The records, after pruning out undesired records.</returns>
        List<T> Prune(List<T> records, bool isFinalPrune);
    }
}
