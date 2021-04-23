using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICatalogLeafToCsvDriver<T> where T : ICsvRecord
    {
        /// <summary>
        /// Whether or not the <see cref="ProcessLeafAsync(CatalogLeafItem, int)"/> should only be called once per
        /// package ID in the catalog scan. Returning <c>true</c> means you expect to process latest data once per 
        /// package ID. Returning <c>false</c> means you expect to process the latest data once per package ID and
        /// version.
        /// </summary>
        bool SingleMessagePerId { get; }

        /// <summary>
        /// Initialize storage containers and dependencies prior to processing package data.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Process each catalog leaf item and return CSV rows to accumulate in Azure Blob storage.
        /// </summary>
        /// <param name="item">The catalog leaf item to process.</param>
        /// <param name="attemptCount">The current attempt count for this catalog leaf item.</param>
        /// <returns>The result, either try again later or a list of records that will be written to CSV.</returns>
        Task<DriverResult<List<T>>> ProcessLeafAsync(CatalogLeafItem item, int attemptCount);

        /// <summary>
        /// The bucket key storing the results on the corresponding <see cref="ProcessLeafAsync(CatalogLeafItem, int)"/>
        /// invocation. This bucket key will be hashed and used to select a large CSV blob to append results to.
        /// Typically this is a concatenation of the normalized, lowercase package ID and version. This key should be
        /// consistent per package ID or package ID + version to allow for proper data pruning with
        /// <see cref="ICsvStorage{T}.Prune(List{T})"/>.
        /// </summary>
        /// <param name="item">T</param>
        /// <returns>The key used for bucketing returned CSV records.</returns>
        string GetBucketKey(CatalogLeafItem item);
    }
}
