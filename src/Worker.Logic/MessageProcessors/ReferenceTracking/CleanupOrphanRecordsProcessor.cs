// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Insights.ReferenceTracking;

namespace NuGet.Insights.Worker.ReferenceTracking
{
    public class CleanupOrphanRecordsProcessor<T> : ITaskStateMessageProcessor<CleanupOrphanRecordsMessage<T>>
        where T : class, ICsvRecord
    {
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly ReferenceTracker _referenceTracker;
        private readonly CsvTemporaryStorageFactory _csvTemporaryStorageFactory;
        private readonly ICsvTemporaryStorage _csvTemporaryStorage;
        private readonly ICleanupOrphanRecordsAdapter<T> _adapter;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly SchemaSerializer _schemaSerializer;

        public CleanupOrphanRecordsProcessor(
            AutoRenewingStorageLeaseService leaseService,
            ReferenceTracker referenceTracker,
            CsvTemporaryStorageFactory csvTemporaryStorageFactory,
            ICsvResultStorage<T> resultStorage,
            ICleanupOrphanRecordsAdapter<T> adapter,
            TaskStateStorageService taskStateStorageService,
            SchemaSerializer schemaSerializer)
        {
            _leaseService = leaseService;
            _referenceTracker = referenceTracker;
            _csvTemporaryStorageFactory = csvTemporaryStorageFactory;
            _csvTemporaryStorage = _csvTemporaryStorageFactory.Create(resultStorage).Single();
            _adapter = adapter;
            _taskStateStorageService = taskStateStorageService;
            _schemaSerializer = schemaSerializer;
        }

        public async Task<TaskStateProcessResult> ProcessAsync(CleanupOrphanRecordsMessage<T> message, TaskState taskState, long dequeueCount)
        {
            await using (var lease = await _leaseService.TryAcquireAsync(taskState.PartitionKey))
            {
                if (!lease.Acquired)
                {
                    return TaskStateProcessResult.Delay;
                }

                var parameters = (CleanupOrphanRecordsParameters)_schemaSerializer.Deserialize(taskState.Parameters).Data;

                if (parameters.State == CleanupOrphanRecordsState.Created)
                {
                    await _csvTemporaryStorageFactory.InitializeAsync(message.StorageSuffix);
                    await _csvTemporaryStorage.InitializeAsync(message.StorageSuffix);

                    parameters.State = CleanupOrphanRecordsState.FindingOrphans;
                    await UpdateParametersAsync(taskState, parameters);
                }

                if (parameters.State == CleanupOrphanRecordsState.FindingOrphans)
                {
                    const int pageSize = 20;
                    var last = parameters.LastPartitionKey is null ? null : new SubjectReference(parameters.LastPartitionKey, parameters.LastRowKey);
                    var allDeleted = await _referenceTracker.GetDeletedSubjectsAsync(
                        _adapter.OwnerType,
                        _adapter.SubjectType,
                        last: last,
                        take: pageSize);

                    if (allDeleted.Count == 0)
                    {
                        parameters.State = CleanupOrphanRecordsState.StartingAggregate;
                        await UpdateParametersAsync(taskState, parameters);
                    }
                    else
                    {
                        // Check each subject that had an owner edge removed to see if it is an orphan.
                        var orphans = new List<SubjectReference>();
                        var nonOrphans = new List<SubjectReference>();
                        foreach (var subject in allDeleted)
                        {
                            var hasOwners = await _referenceTracker.DoesSubjectHaveOwnersAsync(
                                _adapter.OwnerType,
                                _adapter.SubjectType,
                                subject);

                            (hasOwners ? nonOrphans : orphans).Add(subject);
                        }

                        // Immediately delete the non-orphan records.
                        await _referenceTracker.ClearDeletedSubjectsAsync(_adapter.OwnerType, _adapter.SubjectType, nonOrphans);

                        // Map the orphan subjects to CSV records that can be merged into the the CSV file and used for deletion.
                        var orphanRecords = _adapter.MapToOrphanRecords(orphans);

                        // Append the orphan CSV records to the temporary table to prepare for aggregation.
                        await _csvTemporaryStorage.AppendAsync(
                            message.StorageSuffix,
                            orphanRecords);

                        // Track the subject references that have been appended to the CSV.
                        await _taskStateStorageService.AddAsync(
                            taskState.StorageSuffix,
                            taskState.PartitionKey,
                            orphans
                                .Select(x => $"{taskState.RowKey}" +
                                    $"{ReferenceTracker.Separator}{x.PartitionKey}" +
                                    $"{ReferenceTracker.Separator}{x.RowKey}")
                                .ToList());

                        parameters.LastPartitionKey = allDeleted.Last().PartitionKey;
                        parameters.LastRowKey = allDeleted.Last().RowKey;
                        await UpdateParametersAsync(taskState, parameters);
                        return TaskStateProcessResult.Continue;
                    }
                }

                if (parameters.State == CleanupOrphanRecordsState.StartingAggregate)
                {
                    await _csvTemporaryStorage.StartAggregateAsync(message.CleanupId, message.StorageSuffix);
                    parameters.State = CleanupOrphanRecordsState.Aggregating;
                    await UpdateParametersAsync(taskState, parameters);
                    return TaskStateProcessResult.Delay;
                }

                if (parameters.State == CleanupOrphanRecordsState.Aggregating)
                {
                    if (!await _csvTemporaryStorage.IsAggregateCompleteAsync(message.CleanupId, message.StorageSuffix))
                    {
                        return TaskStateProcessResult.Delay;
                    }

                    parameters.State = CleanupOrphanRecordsState.Finalizing;
                    await UpdateParametersAsync(taskState, parameters);
                }

                if (parameters.State == CleanupOrphanRecordsState.Finalizing)
                {
                    await _csvTemporaryStorage.FinalizeAsync(message.StorageSuffix);
                    await _csvTemporaryStorageFactory.FinalizeAsync(message.StorageSuffix);

                    parameters.State = CleanupOrphanRecordsState.Deleting;
                    await UpdateParametersAsync(taskState, parameters);
                }

                if (parameters.State == CleanupOrphanRecordsState.Deleting)
                {
                    var rowKeyPrefix = $"{taskState.RowKey}{ReferenceTracker.Separator}";
                    var orphanTaskStates = await _taskStateStorageService.GetByRowKeyPrefixAsync(
                        taskState.StorageSuffix,
                        taskState.PartitionKey,
                        rowKeyPrefix);
                    var orphans = orphanTaskStates
                        .Select(x => x.RowKey.Substring(rowKeyPrefix.Length))
                        .Select(x => x.Split(ReferenceTracker.Separator, 2))
                        .Select(x => new SubjectReference(x[0], x[1]))
                        .ToList();

                    await _referenceTracker.ClearDeletedSubjectsAsync(_adapter.OwnerType, _adapter.SubjectType, orphans);
                    await _taskStateStorageService.DeleteAsync(orphanTaskStates);

                    parameters.State = CleanupOrphanRecordsState.Complete;
                    await UpdateParametersAsync(taskState, parameters);
                }

                if (parameters.State == CleanupOrphanRecordsState.Complete)
                {
                    return TaskStateProcessResult.Complete;
                }

                return TaskStateProcessResult.Delay;
            }
        }

        private async Task UpdateParametersAsync(TaskState taskState, CleanupOrphanRecordsParameters parameters)
        {
            taskState.Parameters = _schemaSerializer.Serialize(parameters).AsString();
            await _taskStateStorageService.UpdateAsync(taskState);
        }
    }
}
