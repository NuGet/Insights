// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kusto.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
#if ENABLE_CRYPTOAPI
using NuGet.Insights.Worker.PackageCertificateToCsv;
#endif
using NuGet.Insights.Worker.PackageSignatureToCsv;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoDataValidator
    {
        private readonly ICslQueryProvider _queryProvider;
        private readonly IReadOnlyDictionary<Type, ICsvResultStorage> _typeToStorage;
        private readonly CsvResultStorageContainers _containers;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<KustoDataValidator> _logger;

        public KustoDataValidator(
            ICslQueryProvider queryProvider,
            IEnumerable<ICsvResultStorage> csvResultStorage,
            CsvResultStorageContainers containers,
            IOptions<NuGetInsightsWorkerSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<KustoDataValidator> logger)
        {
            _queryProvider = queryProvider;
            _typeToStorage = csvResultStorage.ToDictionary(x => x.RecordType);
            _containers = containers;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task ValidateAsync()
        {
            var validations = new ConcurrentBag<Validation>(Enumerable
                .Empty<Validation>()
#if ENABLE_CRYPTOAPI
                .Concat(GetFingerprintValidations())
                .Concat(GetFingerprintSHA256Validations())
#endif
                .Concat(GetIdentityValidations())
                .Concat(GetLowerIdValidations()));

            var failures = 0;

            await Task.WhenAll(Enumerable
                .Range(0, 8)
                .Select(async x =>
                {
                    while (validations.TryTake(out var validation))
                    {
                        var clientRequestId = Guid.NewGuid().ToString();
                        _logger.LogInformation(
                            "Executing {ValidationLabel} in Kusto with client request ID {ClientRequestId}.",
                            validation.Label,
                            clientRequestId);
                        var stopwatch = Stopwatch.StartNew();
                        var dataReader = await _queryProvider.ExecuteQueryAsync(
                            _options.Value.KustoDatabaseName,
                            validation.Query,
                            new ClientRequestProperties { ClientRequestId = clientRequestId });
                        stopwatch.Stop();
                        _logger.LogInformation(
                            "Completed {ValidationLabel} in Kusto with client request ID {ClientRequestId} in {ElapsedMs}ms.",
                            validation.Label,
                            clientRequestId,
                            stopwatch.ElapsedMilliseconds);

                        var errorMessage = validation.Validate(dataReader);

                        _telemetryClient.TrackMetric(
                            $"{nameof(KustoDataValidator)}.{nameof(ValidateAsync)}.DurationMs",
                            stopwatch.ElapsedMilliseconds,
                            new Dictionary<string, string>
                            {
                                { "ValidationLabel", validation.Label },
                                { "Success", errorMessage == null ? "true" : "false" },
                            });

                        if (errorMessage != null)
                        {
                            _logger.LogError(
                                $"A Kusto validation query failed.{Environment.NewLine}" +
                                $"Validation label: {{ValidationLabel}}{Environment.NewLine}" +
                                $"Error: {{ErrorMessage}}{Environment.NewLine}" +
                                $"{{Query}}",
                                validation.Label,
                                errorMessage,
                                validation.Query);
                            Interlocked.Increment(ref failures);
                        }
                    }
                })
                .ToList());

            if (failures > 0)
            {
                throw new InvalidOperationException("At least one Kusto validation failed. Inspect the error logs.");
            }
        }

        private IEnumerable<Validation> GetIdentityValidations()
        {
            return GetSetValidations(nameof(PackageRecord.Identity), required: true);
        }

        private IEnumerable<Validation> GetLowerIdValidations()
        {
            return GetSetValidations(nameof(PackageRecord.LowerId), required: true);
        }

#if ENABLE_CRYPTOAPI
        private IEnumerable<Validation> GetFingerprintValidations()
        {
            return GetSetValidations(nameof(CertificateRecord.Fingerprint), required: false);
        }

        private IEnumerable<Validation> GetFingerprintSHA256Validations()
        {
            var leftTable = _containers.GetTempKustoTableName(_typeToStorage[typeof(PackageSignature)].ContainerName);
            var rightTable = _containers.GetTempKustoTableName(_typeToStorage[typeof(CertificateRecord)].ContainerName);
            var column = nameof(CertificateRecord.FingerprintSHA256Hex);
            var joinQuery = @$"{leftTable}
| mv-expand {column} = pack_array(
    AuthorSHA256,
    AuthorTimestampSHA256,
    RepositorySHA256,
    RepositoryTimestampSHA256) to typeof(string)
| where isnotempty({column})
| distinct {column}
| join kind=leftouter {rightTable}";

            yield return GetLeftRightValidation(leftTable, rightTable, column, "left outer", joinQuery);
        }
#endif

        private IEnumerable<Validation> GetSetValidations(string column, bool required)
        {
            var tables = _typeToStorage
                .Values
                .Where(x => x.RecordType.GetProperty(column) != null)
                .Select(x => _containers.GetTempKustoTableName(x.ContainerName))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();
            var leftTable = tables.First();
            var validations = new List<Validation>();

            foreach (var rightTable in tables.Skip(1))
            {
                var joinQuery = @$"{leftTable}{Environment.NewLine}{(required ? string.Empty : $"| where isnotempty({column})")}
| distinct {column}
| join kind=fullouter (
    {rightTable}{Environment.NewLine}{(required ? string.Empty : $"    | where isnotempty({column})")}
    | distinct {column}
)";

                yield return GetLeftRightValidation(leftTable, rightTable, column, "full outer", joinQuery);
            }
        }

        private Validation GetLeftRightValidation(string leftTable, string rightTable, string column, string comparisonType, string joinQuery)
        {
            var query = $@"{joinQuery} on {column}
| where isempty({column}) or isempty({column}1)
| summarize
    LeftOnlyCount = countif(isnotempty({column})),
    LeftOnlySample = make_set_if({column}, isnotempty({column}), 5),
    RightOnlyCount = countif(isnotempty({column}1)),
    RightOnlySample = make_set_if({column}1, isnotempty({column}1), 5)";

            return new Validation(
                $"{comparisonType} set comparison of {leftTable}.{column} and {rightTable}.{column}",
                query,
                dataReader =>
                {
                    if (!dataReader.Read())
                    {
                        return "The query did not return any rows.";
                    }

                    var leftOnlyCount = dataReader.GetInt64(0);
                    var leftOnlySample = (JValue)dataReader.GetValue(1);
                    var rightOnlyCount = dataReader.GetInt64(2);
                    var rightOnlySample = (JValue)dataReader.GetValue(3);

                    if (leftOnlyCount != 0 || rightOnlyCount != 0)
                    {
                        return
                            $"The set of values in the {column} columns in the {leftTable} and {rightTable} tables do not match.{Environment.NewLine}" +
                            $"{column} values in {leftTable} but not {rightTable}:{Environment.NewLine}" +
                            $"  - Count: {leftOnlyCount}{Environment.NewLine}" +
                            $"  - Sample: {leftOnlySample}{Environment.NewLine}" +
                            $"{column} values in {rightTable} but not {leftTable}:{Environment.NewLine}" +
                            $"  - Count: {rightOnlyCount}{Environment.NewLine}" +
                            $"  - Sample: {rightOnlySample}{Environment.NewLine}";
                    }

                    if (dataReader.Read())
                    {
                        return "The query returned more than one row.";
                    }

                    return null;
                });
        }

        private record Validation(string Label, string Query, Func<IDataReader, string> Validate);
    }
}
