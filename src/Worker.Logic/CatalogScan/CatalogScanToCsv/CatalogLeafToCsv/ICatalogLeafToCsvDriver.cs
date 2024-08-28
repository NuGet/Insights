// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public interface ICatalogLeafToCsvDriver
    {
        /// <summary>
        /// Whether or not the <see cref="ICatalogLeafToCsvDriver{T}.ProcessLeafAsync(CatalogLeafScan)"/> or
        /// <see cref="ICatalogLeafToCsvBatchDriver{T}.ProcessLeavesAsync(IReadOnlyList{CatalogLeafScan})"/>
        /// should only be called once per package ID in the catalog scan. Returning <c>true</c> means you expect to
        /// process latest data once per package ID. Returning <c>false</c> means you expect to process the latest data
        /// once per package ID and version.
        /// </summary>
        bool SingleMessagePerId { get; }

        /// <summary>
        /// Initialize storage containers and dependencies prior to processing package data.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Delete additional output storage aside from the storage container referenced by <see cref="ICsvResultStorage{T}.ResultContainerName"/>.
        /// </summary>
        Task DestroyAsync();
    }

    public interface ICatalogLeafToCsvDriver<T> : ICatalogLeafToCsvDriver where T : class, IAggregatedCsvRecord<T>
    {
        /// <summary>
        /// Process each catalog leaf scan and return CSV rows to accumulate in Azure Blob storage.
        /// </summary>
        /// <param name="leafScan">The catalog leaf scan to process.</param>
        /// <returns>The result, either try again later or a list of records that will be written to CSV.</returns>
        Task<DriverResult<IReadOnlyList<T>>> ProcessLeafAsync(CatalogLeafScan leafScan);
    }

    public interface ICatalogLeafToCsvDriver<T1, T2> : ICatalogLeafToCsvDriver
        where T1 : class, IAggregatedCsvRecord<T1>
        where T2 : class, IAggregatedCsvRecord<T2>
    {
        /// <summary>
        /// Process each catalog leaf scan and return CSV rows to accumulate in Azure Blob storage.
        /// </summary>
        /// <param name="leafScan">The catalog leaf scan to process.</param>
        /// <returns>The result, either try again later or a list of records that will be written to CSV.</returns>
        Task<DriverResult<CsvRecordSets<T1, T2>>> ProcessLeafAsync(CatalogLeafScan leafScan);
    }

    public interface ICatalogLeafToCsvDriver<T1, T2, T3> : ICatalogLeafToCsvDriver
        where T1 : class, IAggregatedCsvRecord<T1>
        where T2 : class, IAggregatedCsvRecord<T2>
        where T3 : class, IAggregatedCsvRecord<T3>
    {
        /// <summary>
        /// Process each catalog leaf scan and return CSV rows to accumulate in Azure Blob storage.
        /// </summary>
        /// <param name="leafScan">The catalog leaf scan to process.</param>
        /// <returns>The result, either try again later or a list of records that will be written to CSV.</returns>
        Task<DriverResult<CsvRecordSets<T1, T2, T3>>> ProcessLeafAsync(CatalogLeafScan leafScan);
    }
}
