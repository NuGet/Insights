using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Worker
{
    public interface ICsvCompactor<T> where T : ICsvRecord<T>, new()
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
    }
}
