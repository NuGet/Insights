// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public delegate List<T> Prune<T>(List<T> records, bool removeDeleted) where T : ICsvRecord;

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
        /// For record sets that always leave a marker record for deleted data (e.g. package-based records), the
        /// <paramref name="removeDeleted"/> parameter can be ignored. For record sets that do not keep any reference
        /// to deleted data, this method should only remove records for deleted data when <paramref name="removeDeleted"/>
        /// is true.
        /// </summary>
        /// <param name="records">The records to prune.</param>
        /// <param name="removeDeleted">Whether or not deleted records should be removed. If false, the prune logic should just remove duplicates.</param>
        /// <returns>The records, after pruning out undesired records.</returns>
        List<T> Prune(List<T> records, bool removeDeleted);

        /// <summary>
        /// Given a previously persisted CSV record, a catalog leaf item is returned if the record should be reprocessed.
        /// This method is only invoked during a special "reprocess" flow started by <see cref="CatalogScanService.ReprocessAsync(CatalogScanDriverType)"/>.
        /// </summary>
        /// <param name="record">The record to test for reprocessing.</param>
        /// <returns>A catalog leaf item that should be reprocessing, null if the record should not be reprocessed.</returns>
        Task<ICatalogLeafItem> MakeReprocessItemOrNullAsync(T record);
    }
}
