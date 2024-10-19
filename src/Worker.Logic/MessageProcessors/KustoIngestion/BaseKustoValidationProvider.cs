// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public abstract class BaseKustoValidationProvider
    {
        protected readonly CsvRecordContainers _containers;

        public BaseKustoValidationProvider(CsvRecordContainers containers)
        {
            _containers = containers;
        }

        protected async Task<bool> HasBlobsAsync(CsvRecordContainerInfo info)
        {
            var blobs = await _containers.GetBlobsAsync(info.ContainerName);
            return blobs.Count > 0;
        }

        protected async Task<IReadOnlyList<KustoValidation>> GetSetValidationsAsync(string column, bool required)
        {
            var containersWithBlobs = new List<CsvRecordContainerInfo>();
            foreach (var info in _containers.ContainerInfo)
            {
                if (await HasBlobsAsync(info))
                {
                    containersWithBlobs.Add(info);
                }
            }

            var tables = containersWithBlobs
                .Where(x => x.RecordType.GetProperty(column) != null)
                .Select(x => _containers.GetTempKustoTableName(x.ContainerName))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            if (tables.Count <= 1)
            {
                return Array.Empty<KustoValidation>();
            }

            var leftTable = tables.First();
            var validations = new List<KustoValidation>();

            foreach (var rightTable in tables.Skip(1))
            {
                var joinQuery = @$"{leftTable}{(required ? string.Empty : $"{Environment.NewLine}| where isnotempty({column})")}
| distinct {column}
| join kind=fullouter (
    {rightTable}{(required ? string.Empty : $"{Environment.NewLine}    | where isnotempty({column})")}
    | distinct {column}
)";

                validations.Add(GetLeftRightValidation(leftTable, rightTable, column, "full outer", joinQuery));
            }

            return validations;
        }

        protected static KustoValidation GetLeftRightValidation(string leftTable, string rightTable, string column, string comparisonType, string joinQuery)
        {
            var query = $@"{joinQuery} on {column}
| where isempty({column}) or isempty({column}1)
| summarize
    LeftOnlyCount = countif(isnotempty({column})),
    LeftOnlySample = make_set_if({column}, isnotempty({column}), 5),
    RightOnlyCount = countif(isnotempty({column}1)),
    RightOnlySample = make_set_if({column}1, isnotempty({column}1), 5)";

            return new KustoValidation(
                $"{comparisonType} set comparison of {leftTable}.{column} and {rightTable}.{column}",
                query,
                dataReader =>
                {
                    if (!dataReader.Read())
                    {
                        return "The query did not return any rows.";
                    }

                    var leftOnlyCount = dataReader.GetInt64(0);
                    var leftOnlySample = (JToken)dataReader.GetValue(1);
                    var rightOnlyCount = dataReader.GetInt64(2);
                    var rightOnlySample = (JToken)dataReader.GetValue(3);

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
    }
}
