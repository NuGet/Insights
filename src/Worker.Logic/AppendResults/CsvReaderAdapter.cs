// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Buffers;
using Sylvan.Data.Csv;

namespace NuGet.Insights.Worker
{
    public class CsvReaderAdapter : ICsvReader
    {
        private static readonly ConcurrentDictionary<Type, string> TypeToHeader = new ConcurrentDictionary<Type, string>();

        /// <summary>
        /// Needed due to these big guys:
        /// - GR.PageRender.Razor 1.6.0 - massive number of content files, ~2,078,825 bytes
        /// - GR.PageRender.Razor 1.7.0 - massive number of content files, ~2,095,119 bytes
        /// </summary>
        public const int MaxBufferSize = 4 * 1024 * 1024;

        private readonly ILogger<CsvReaderAdapter> _logger;

        public CsvReaderAdapter(ILogger<CsvReaderAdapter> logger)
        {
            _logger = logger;
        }

        public string GetHeader<T>() where T : ICsvRecord
        {
            var type = typeof(T);
            return TypeToHeader.GetOrAdd(type, _ =>
            {
                var headerWriter = Activator.CreateInstance<T>();
                using var stringWriter = new StringWriter();
                headerWriter.WriteHeader(stringWriter);
                return stringWriter.ToString().TrimEnd();
            });
        }

        public CsvReaderResult<T> GetRecords<T>(TextReader reader, int bufferSize) where T : ICsvRecord
        {
            var actualHeader = reader.ReadLine();
            var expectedHeader = GetHeader<T>();
            if (actualHeader != expectedHeader)
            {
                throw new InvalidOperationException(
                    "The header in the blob doesn't match the header for the readers being added." + Environment.NewLine +
                    "Expected: " + expectedHeader + Environment.NewLine +
                    "Actual: " + actualHeader);
            }

            var allRecords = new List<T>();
            var pool = ArrayPool<char>.Shared;
            var buffer = pool.Rent(bufferSize);
            try
            {
                using var csvReader = CsvDataReader.Create(reader, buffer, new CsvDataReaderOptions
                {
                    HasHeaders = false,
                });

                var factory = Activator.CreateInstance<T>();

                while (csvReader.Read())
                {
                    var i = 0;
                    var record = (T)factory.ReadNew(() => csvReader.GetString(i++));
                    allRecords.Add(record);
                }

                return new CsvReaderResult<T>(CsvReaderResultType.Success, allRecords);
            }
            catch (CsvRecordTooLargeException ex) when (bufferSize < MaxBufferSize)
            {
                _logger.LogInformation(
                    ex,
                    "Could not read CSV with buffer size {BufferSize} on row {RowNumBer} and field ordinal {FieldOrdinal}.",
                    bufferSize,
                    ex.RowNumber,
                    ex.FieldOrdinal);
                return new CsvReaderResult<T>(CsvReaderResultType.BufferTooSmall, records: null);
            }
            finally
            {
                pool.Return(buffer);
            }
        }
    }
}
