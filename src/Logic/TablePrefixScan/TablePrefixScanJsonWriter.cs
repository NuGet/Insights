// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Newtonsoft.Json;

namespace NuGet.Insights.TablePrefixScan
{
    public class TablePrefixScanJsonWriter
    {
        private readonly TablePrefixScanner _scanner;

        public TablePrefixScanJsonWriter(TablePrefixScanner scanner)
        {
            _scanner = scanner;
        }

        public async Task WriteAsync(string path, TableClient table, TablePrefixScanJsonType type, int segmentsPerFirstPrefix, int segmentsPerSubsequentPrefix)
        {
            StepCollection remainingSteps;
            switch (type)
            {
                case TablePrefixScanJsonType.BreadthFirstSearch:
                    remainingSteps = new BFS();
                    break;
                case TablePrefixScanJsonType.DepthFirstSearch:
                    remainingSteps = new DFS();
                    break;
                default:
                    throw new NotImplementedException();
            }

            var queryParameters = new TableQueryParameters(table, StorageUtility.MinSelectColumns, 2, expandPartitionKeys: true);
            var firstPrefix = "";
            remainingSteps.Add(null, new TablePrefixScanStart(queryParameters, firstPrefix));

            var completedSteps = new List<object>();
            var entityCount = 0;
            var lastThreshold = 0;
            var nextStepId = 1;
            while (remainingSteps.Any())
            {
                var currentStep = remainingSteps.RemoveNext();
                var currentStepId = nextStepId++;

                List<TablePrefixScanStep> nextSteps;
                object stepData;
                switch (currentStep.Step)
                {
                    case TablePrefixScanStart start:
                        nextSteps = _scanner.Start(start);
                        stepData = new { Type = "Start", start.Depth, start.PartitionKeyPrefix };
                        break;
                    case TablePrefixScanEntitySegment<TableEntity> entitySegment:
                        nextSteps = new List<TablePrefixScanStep>();
                        var first = entitySegment.Entities.First();
                        var last = entitySegment.Entities.Last();
                        entityCount += entitySegment.Entities.Count;
                        stepData = new { Type = "EntitySegment", entitySegment.Depth, First = new { first.PartitionKey, first.RowKey }, Last = new { last.PartitionKey, last.RowKey }, entitySegment.Entities.Count };
                        break;
                    case TablePrefixScanPartitionKeyQuery partitionKeyQuery:
                        nextSteps = await _scanner.ExecutePartitionKeyQueryAsync<TableEntity>(partitionKeyQuery);
                        stepData = new { Type = "PartitionKeyQuery", partitionKeyQuery.Depth, partitionKeyQuery.PartitionKey, partitionKeyQuery.RowKeySkip };
                        break;
                    case TablePrefixScanPrefixQuery prefixQuery:
                        nextSteps = await _scanner.ExecutePrefixQueryAsync<TableEntity>(prefixQuery, segmentsPerFirstPrefix, segmentsPerSubsequentPrefix);
                        stepData = new { Type = "PrefixQuery", prefixQuery.Depth, prefixQuery.PartitionKeyPrefix, prefixQuery.PartitionKeyLowerBound };
                        break;
                    default:
                        throw new NotImplementedException();
                }

                remainingSteps.AddRange(currentStepId, nextSteps);

                completedSteps.Add(new
                {
                    Id = currentStepId,
                    currentStep.ParentId,
                    Timestamp = DateTimeOffset.UtcNow.ToString("O"),
                    Data = stepData,
                });

                var roundedDownEntityCount = entityCount - (entityCount % 1000);
                if (roundedDownEntityCount > lastThreshold)
                {
                    lastThreshold = roundedDownEntityCount;
                    Console.WriteLine(lastThreshold);
                }
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(completedSteps));
        }

        private abstract class StepCollection
        {
            public abstract void Add(int? parentId, TablePrefixScanStep step);
            public abstract void AddRange(int? parentId, IEnumerable<TablePrefixScanStep> steps);
            public abstract bool Any();
            public abstract (int? ParentId, TablePrefixScanStep Step) RemoveNext();
        }

        private class DFS : StepCollection
        {
            private readonly Stack<(int? ParentId, TablePrefixScanStep Step)> _data = new Stack<(int? ParentId, TablePrefixScanStep Step)>();

            public override void Add(int? parentId, TablePrefixScanStep step)
            {
                _data.Push((parentId, step));
            }

            public override void AddRange(int? parentId, IEnumerable<TablePrefixScanStep> steps)
            {
                foreach (var step in steps.Reverse())
                {
                    Add(parentId, step);
                }
            }

            public override bool Any()
            {
                return _data.Any();
            }

            public override (int? ParentId, TablePrefixScanStep Step) RemoveNext()
            {
                return _data.Pop();
            }
        }

        private class BFS : StepCollection
        {
            private readonly Queue<(int? ParentId, TablePrefixScanStep Step)> _data = new Queue<(int? ParentId, TablePrefixScanStep Step)>();

            public override void Add(int? parentId, TablePrefixScanStep step)
            {
                _data.Enqueue((parentId, step));
            }

            public override void AddRange(int? parentId, IEnumerable<TablePrefixScanStep> steps)
            {
                foreach (var step in steps)
                {
                    Add(parentId, step);
                }
            }

            public override bool Any()
            {
                return _data.Any();
            }

            public override (int? ParentId, TablePrefixScanStep Step) RemoveNext()
            {
                return _data.Dequeue();
            }
        }

    }
}
