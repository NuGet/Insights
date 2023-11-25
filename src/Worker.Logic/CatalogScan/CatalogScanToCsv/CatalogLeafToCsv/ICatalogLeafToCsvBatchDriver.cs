// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public interface ICatalogLeafToCsvBatchDriver<T> : ICatalogLeafToCsvDriver where T : class, ICsvRecord
    {
        /// <summary>
        /// Process each catalog leaf item and return CSV rows to accumulate in Azure Blob storage.
        /// </summary>
        /// <param name="leafScans">The catalog leaf scans to process.</param>
        /// <returns>The batch result, either try again later or a list of records that will be written to CSV.</returns>
        Task<BatchMessageProcessorResult<CsvRecordSets<T>, CatalogLeafScan>> ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans);
    }

    public interface ICatalogLeafToCsvBatchDriver<T1, T2> : ICatalogLeafToCsvDriver
        where T1 : class, ICsvRecord
        where T2 : class, ICsvRecord
    {
        /// <summary>
        /// Process each catalog leaf item and return CSV rows to accumulate in Azure Blob storage.
        /// </summary>
        /// <param name="leafScans">The catalog leaf scans to process.</param>
        /// <returns>The batch result, either try again later or a list of records that will be written to CSV.</returns>
        Task<BatchMessageProcessorResult<CsvRecordSets<T1, T2>, CatalogLeafScan>> ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans);
    }

    public interface ICatalogLeafToCsvBatchDriver<T1, T2, T3> : ICatalogLeafToCsvDriver
        where T1 : class, ICsvRecord
        where T2 : class, ICsvRecord
        where T3 : class, ICsvRecord
    {
        /// <summary>
        /// Process each catalog leaf item and return CSV rows to accumulate in Azure Blob storage.
        /// </summary>
        /// <param name="leafScans">The catalog leaf scans to process.</param>
        /// <returns>The batch result, either try again later or a list of records that will be written to CSV.</returns>
        Task<BatchMessageProcessorResult<CsvRecordSets<T1, T2, T3>, CatalogLeafScan>> ProcessLeavesAsync(IReadOnlyList<CatalogLeafScan> leafScans);
    }
}
