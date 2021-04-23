using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICsvStorage<T> where T : ICsvRecord
    {
        /// <summary>
        /// The Azure Blob Storage container name to write CSV results to.
        /// </summary>
        string ResultsContainerName { get; }

        /// <summary>
        /// Prune the provided records to remove duplicate or old data. Packages are unlisted, relisted, reflowed, or
        /// appear in the catalog again for some reason, this method is called to prune out duplicate CSV records.
        /// </summary>
        /// <param name="records">The records to prune.</param>
        /// <returns>The records, after pruning out undesired records.</returns>
        List<T> Prune(List<T> records);

        /// <summary>
        /// Given a previously persisted CSV record, a catalog leaf item is returned if the record should be reprocessed.
        /// This method is only invoked during a special "reprocess" flow started by <see cref="CatalogScanService.ReprocessAsync(CatalogScanDriverType)"/>.
        /// </summary>
        /// <param name="record">The record to test for reprocessing.</param>
        /// <returns>A catalog leaf item that should be reprocessing, null if the record should not be reprocessed.</returns>
        Task<CatalogLeafItem> MakeReprocessItemOrNullAsync(T record);
    }
}
