// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using MessagePack;
using Microsoft.Extensions.Options;
using NuGet.Insights.StorageNoOpRetry;
using NuGet.Insights.WideEntities;
using NuGet.Packaging;

namespace NuGet.Insights.ReferenceTracking
{
    public class ReferenceTracker
    {
        public const char Separator = '$';
        private readonly WideEntityService _wideEntityService;
        private readonly ServiceClientFactory _clientFactory;
        private readonly IOptions<NuGetInsightsSettings> _options;

        public static IReadOnlySet<SubjectEdge> EmptySet { get; } = new HashSet<SubjectEdge>();

        public ReferenceTracker(
            WideEntityService wideEntityService,
            ServiceClientFactory clientFactory,
            IOptions<NuGetInsightsSettings> options)
        {
            _wideEntityService = wideEntityService;
            _clientFactory = clientFactory;
            _options = options;
        }

        public async Task InitializeAsync(string ownerToSubjectTableName, string subjectToOwnerTableName)
        {
            await (await GetOwnerToSubjectTableAsync(ownerToSubjectTableName)).CreateIfNotExistsAsync(retry: true);
            await (await GetSubjectToOwnerTableAsync(subjectToOwnerTableName)).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DestroyAsync(string ownerToSubjectTableName, string subjectToOwnerTableName)
        {
            await (await GetOwnerToSubjectTableAsync(ownerToSubjectTableName)).DeleteAsync();
            await (await GetSubjectToOwnerTableAsync(subjectToOwnerTableName)).DeleteAsync();
        }

        private async Task<TableClientWithRetryContext> GetOwnerToSubjectTableAsync(string ownerToSubjectTableName)
        {
            return (await _clientFactory.GetTableServiceClientAsync()).GetTableClient(ownerToSubjectTableName);
        }

        private async Task<TableClientWithRetryContext> GetSubjectToOwnerTableAsync(string subjectToOwnerTableName)
        {
            return (await _clientFactory.GetTableServiceClientAsync()).GetTableClient(subjectToOwnerTableName);
        }

        public async Task<IReadOnlyList<OwnerReference>> GetOwnersOfSubjectAsync(
            string subjectToOwnerTableName,
            string ownerType,
            string subjectType,
            SubjectReference subject)
        {
            (var entities, var prefix) = await GetSubjectToOwnerReferencesAsync(subjectToOwnerTableName, ownerType, subjectType, subject);
            var owners = new List<OwnerReference>();
            await foreach (var entity in entities)
            {
                var ownerPartitionKey = entity.PartitionKey.Substring(prefix.Length);
                var ownerRowKey = entity.RowKey;
                owners.Add(new OwnerReference(ownerPartitionKey, ownerRowKey));
            }

            return owners;
        }

        public async Task<IReadOnlyList<SubjectReference>> GetDeletedSubjectsAsync(
            string subjectToOwnerTableName,
            string ownerType,
            string subjectType)
        {
            return await GetDeletedSubjectsAsync(subjectToOwnerTableName, ownerType, subjectType, last: null, take: null);
        }

        public async Task<IReadOnlyList<SubjectReference>> GetDeletedSubjectsAsync(
            string subjectToOwnerTableName,
            string ownerType,
            string subjectType,
            SubjectReference last,
            int? take)
        {
            GuardNoSeparator(nameof(ownerType), ownerType);
            GuardNoSeparator(nameof(subjectType), subjectType);

            var subjectToOwnerTable = await GetSubjectToOwnerTableAsync(subjectToOwnerTableName);
            var partitionKey = GetDeletePartitionKey(ownerType, subjectType);
            IAsyncEnumerable<TableEntity> entities;

            if (last is null)
            {
                entities = subjectToOwnerTable.QueryAsync<TableEntity>(
                    filter: x => x.PartitionKey == partitionKey,
                    select: new[] { StorageUtility.RowKey });
            }
            else
            {
                var lastRowKey = GetDeleteRowKey(last);
                entities = subjectToOwnerTable.QueryAsync<TableEntity>(
                    filter: x => x.PartitionKey == partitionKey && x.RowKey.CompareTo(lastRowKey) > 0,
                    select: new[] { StorageUtility.RowKey });
            }

            if (take.HasValue)
            {
                entities = entities.Take(take.Value);
            }

            var deleted = new List<SubjectReference>();
            await foreach (var entity in entities)
            {
                var pieces = entity.RowKey.Split(new[] { Separator }, 2);
                deleted.Add(new SubjectReference(pieces[0], pieces[1]));
            }

            return deleted;
        }

        public async Task<bool> DoesSubjectHaveOwnersAsync(
            string subjectToOwnerTableName,
            string ownerType,
            string subjectType,
            SubjectReference subject)
        {
            (var entities, _) = await GetSubjectToOwnerReferencesAsync(subjectToOwnerTableName, ownerType, subjectType, subject, maxPerPage: 1);
            return await entities.AnyAsync();
        }

        public async Task ClearDeletedSubjectsAsync(
            string subjectToOwnerTableName,
            string ownerType,
            string subjectType,
            IReadOnlyList<SubjectReference> subjects)
        {
            GuardNoSeparator(nameof(ownerType), ownerType);
            GuardNoSeparator(nameof(subjectType), subjectType);

            var subjectToOwnerTable = await GetSubjectToOwnerTableAsync(subjectToOwnerTableName);
            var batch = new MutableTableTransactionalBatch(subjectToOwnerTable);
            var partitionKey = GetDeletePartitionKey(ownerType, subjectType);
            foreach (var subject in subjects)
            {
                var rowKey = GetDeleteRowKey(subject);
                batch.DeleteEntity(partitionKey, rowKey, ETag.All);
                if (batch.Count >= StorageUtility.MaxBatchSize)
                {
                    await batch.SubmitBatchAsync();
                    batch = new MutableTableTransactionalBatch(subjectToOwnerTable);
                }
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        private async Task<(AsyncPageable<TableEntity> Entities, string Prefix)> GetSubjectToOwnerReferencesAsync(
            string subjectToOwnerTableName,
            string ownerType,
            string subjectType,
            SubjectReference subject,
            int? maxPerPage = null)
        {
            GuardNoSeparator(nameof(ownerType), ownerType);
            GuardNoSeparator(nameof(subjectType), subjectType);

            var prefix = GetSubjectToOwnerPartitionKeyPrefix(ownerType, subjectType, subject);
            var subjectToOwnerTable = await GetSubjectToOwnerTableAsync(subjectToOwnerTableName);
            var entities = subjectToOwnerTable.QueryAsync<TableEntity>(
                filter: x => x.PartitionKey.CompareTo(prefix) >= 0 && x.PartitionKey.CompareTo(prefix + char.MaxValue) < 0,
                select: new[] { StorageUtility.PartitionKey, StorageUtility.RowKey },
                maxPerPage: maxPerPage);

            return (entities, prefix);
        }

        public async Task SetReferencesAsync(
            string ownerToSubjectTableName,
            string subjectToOwnerTableName,
            string ownerType,
            string subjectType,
            string ownerPartitionKey,
            IReadOnlyDictionary<string, IReadOnlySet<SubjectEdge>> ownerRowKeyToSubjects)
        {
            if (ownerRowKeyToSubjects.Count == 0)
            {
                throw new ArgumentException("There must be at least one owner row key being operated on.", nameof(ownerRowKeyToSubjects));
            }

            GuardNoSeparator(nameof(ownerType), ownerType);
            GuardNoSeparator(nameof(subjectType), subjectType);
            GuardNoSeparator(nameof(ownerPartitionKey), ownerPartitionKey);
            foreach (var pair in ownerRowKeyToSubjects)
            {
                GuardNoSeparator("row keys in the " + nameof(ownerRowKeyToSubjects), pair.Key);
            }

            var subjectToOwnerTable = await GetSubjectToOwnerTableAsync(subjectToOwnerTableName);
            var context = new SetReferencesContext(
                ownerToSubjectTableName,
                subjectToOwnerTable,
                ownerType,
                subjectType,
                ownerPartitionKey,
                ownerRowKeyToSubjects);

            await PrepareOwnerToSubjectAsync(context);
            await UpdateSubjectToOwnerAsync(context);
            await RecordSubjectsWithToDeleteAsync(context);
            await CommitOwnerToSubjectAsync(context);
        }

        private async Task PrepareOwnerToSubjectAsync(SetReferencesContext context)
        {
            var rowKeys = context
                .OwnerRowKeyToSubjects
                .Keys
                .OrderBy(x => x + WideEntitySegment.RowKeySeparator, StringComparer.Ordinal)
                .ToList();

            var existingEntities = await _wideEntityService.RetrieveAsync(
                context.OwnerToSubjectTableName,
                context.OwnerToSubjectPartitionKey,
                rowKeys.First(),
                rowKeys.Last());
            var rowKeyToExistingEntity = existingEntities
                .Where(x => context.OwnerRowKeyToSubjects.ContainsKey(x.RowKey))
                .ToDictionary(x => x.RowKey);

            var batch = new List<WideEntityOperation>();
            foreach (var ownerRowKey in rowKeys)
            {
                var newSubjects = context.OwnerRowKeyToSubjects[ownerRowKey];
                var newReferences = GetReferences(newSubjects);
                OwnerToSubjectEdges newData;

                // Get the existing data, if any, and discover edge changes.
                OwnerToSubjectEdges existingData = null;
                if (rowKeyToExistingEntity.TryGetValue(ownerRowKey, out var existingEntity))
                {
                    existingData = Deserialize<OwnerToSubjectEdges>(existingEntity);
                    var committedReferences = GetReferences(existingData.Committed);

                    // We want to add all new references that are not already committed or re-add uncommitted
                    // deletes. Anything in the "ToDelete" category was once in the "Committed" category and may
                    // have its subject-to-owner references removed.
                    var toAdd = new HashSet<SubjectReference>(newReferences);
                    toAdd.ExceptWith(committedReferences.Except(existingData.ToDelete));

                    // We want to remove all committed references that are not also in the new reference set as
                    // well as any uncommitted references. Anything in the "ToAdd" category may have undesired
                    // subject-to-owner references added but never committed in the owner-to-subject entity.
                    var toDelete = new HashSet<SubjectReference>(committedReferences);
                    toDelete.UnionWith(existingData.ToAdd);
                    toDelete.ExceptWith(newReferences);

                    newData = new OwnerToSubjectEdges
                    {
                        // We keep the existing "Committed" set since this is only the beginning of the update
                        // operation. At the end, we'll actually update the "Committed" set to a new value.
                        Committed = existingData.Committed,
                        ToAdd = SortReferences(toAdd),
                        ToDelete = SortReferences(toDelete),
                    };
                }
                else
                {
                    newData = new OwnerToSubjectEdges()
                    {
                        Committed = Array.Empty<SubjectEdge>(),
                        ToAdd = SortReferences(newReferences),
                        ToDelete = Array.Empty<SubjectReference>(),
                    };
                }

                if (newData.ToAdd.Count == 0 && newData.ToDelete.Count == 0)
                {
                    if (existingData == null)
                    {
                        // No new references are being added and no existing data exists. We can skip this row key
                        // entirely.
                        continue;
                    }
                    else
                    {
                        var fullComparison = new HashSet<SubjectEdge>(newSubjects);
                        if (fullComparison.SetEquals(existingData.Committed))
                        {
                            // The new references match the existing data exactly. We can skip this row key entirely.
                            continue;
                        }
                        else
                        {
                            // The only thing that has changes is the data in the existing references. We don't need to
                            // update this owner-to-subject entity until after other subject-to-owner entities have been
                            // updated.
                            context.OwnerRowKeyToInfo.Add(ownerRowKey, new OwnerToSubjectInfo(newData)
                            {
                                Entity = existingEntity,
                            });
                            continue;
                        }
                    }
                }

                context.OwnerRowKeyToInfo.Add(ownerRowKey, new OwnerToSubjectInfo(newData));

                var newBytes = Serialize(newData);
                if (existingEntity != null)
                {
                    batch.Add(WideEntityOperation.Replace(existingEntity, newBytes));
                }
                else
                {
                    batch.Add(WideEntityOperation.Insert(context.OwnerToSubjectPartitionKey, ownerRowKey, newBytes));
                }

                EntityReference IdentityToReference(IReference subject)
                {
                    return new EntityReference(context.OwnerPartitionKey, ownerRowKey, subject.PartitionKey, subject.RowKey);
                }

                context.ToAdd.AddRange(newData.ToAdd.Select(IdentityToReference));
                context.ToDelete.AddRange(newData.ToDelete.Select(IdentityToReference));
            }

            // Persistence step 1: update the owner-to-subject references to keep the current committed set but track
            // the uncommitted adds and deletes.
            var result = await _wideEntityService.ExecuteBatchAsync(context.OwnerToSubjectTableName, batch, allowBatchSplits: true);
            foreach (var wideEntity in result)
            {
                if (context.OwnerRowKeyToInfo.TryGetValue(wideEntity.RowKey, out var cleanupContext))
                {
                    cleanupContext.Entity = wideEntity;
                }
            }
        }

        private static IReadOnlyList<SubjectReference> GetReferences(IEnumerable<SubjectEdge> newSubjects)
        {
            return newSubjects
                .Select(x => new SubjectReference(x.PartitionKey, x.RowKey))
                .ToList();
        }

        private static IReadOnlyList<T> SortReferences<T>(IEnumerable<T> references) where T : IReference
        {
            if (references is ICollection<T> collection && collection.Count == 0)
            {
                return Array.Empty<T>();
            }

            return references
                .OrderBy(x => x.RowKey, StringComparer.Ordinal)
                .ThenBy(x => x.PartitionKey, StringComparer.Ordinal)
                .ToList();
        }

        private static async Task UpdateSubjectToOwnerAsync(SetReferencesContext context)
        {
            // Persistence step 2: adding and remove subject-to-owner references discovered in the previous step.
            var groups = context
                .ToAdd
                .Select(x => new { Add = true, Reference = x })
                .Concat(context.ToDelete.Select(x => new { Add = false, Reference = x }))
                .GroupBy(x => new SubjectReference(x.Reference.SubjectPartitionKey, x.Reference.SubjectRowKey));
            foreach (var group in groups)
            {
                var partitionKey = context.GetSubjectToOwnerPartitionKey(group.Key);
                var batch = new MutableTableTransactionalBatch(context.SubjectToOwnerTable);
                var operations = group.ToHashSet();

                foreach (var operation in operations.Where(x => x.Add).ToList())
                {
                    var rowKey = GetSubjectToOwnerRowKey(operation.Reference.OwnerRowKey);
                    batch.UpsertEntity(new TableEntity(partitionKey, rowKey), TableUpdateMode.Replace);
                    if (batch.Count >= StorageUtility.MaxBatchSize)
                    {
                        await batch.SubmitBatchAsync();
                        batch = new MutableTableTransactionalBatch(context.SubjectToOwnerTable);
                    }
                }

                await batch.SubmitBatchIfNotEmptyAsync();

                // Delete the items outside of the batch since deletion of non-existent items in a batch causes a full rollback.
                foreach (var operation in operations.Where(x => !x.Add).ToList())
                {
                    var rowKey = GetSubjectToOwnerRowKey(operation.Reference.OwnerRowKey);
                    await context.SubjectToOwnerTable.DeleteEntityAsync(partitionKey, rowKey, ifMatch: ETag.All);
                }
            }
        }

        private static async Task RecordSubjectsWithToDeleteAsync(SetReferencesContext context)
        {
            // Persistence step 3: record all subjects that only appear in "ToDelete". This data will be used later to
            // find orphaned subjects for clean-up. "ToAdd" and "ToDelete" may overlap if a subject was deleted from one
            // owner but added to another.
            var toDeleteSubjects = context.ToDelete.Select(x => new SubjectReference(x.SubjectPartitionKey, x.SubjectRowKey));
            var toAddSubjects = context.ToAdd.Select(x => new SubjectReference(x.SubjectPartitionKey, x.SubjectRowKey));
            var toDeleteOnlySubjects = toDeleteSubjects.Except(toAddSubjects);

            var batch = new MutableTableTransactionalBatch(context.SubjectToOwnerTable);
            var partitionKey = GetDeletePartitionKey(context.OwnerType, context.SubjectType);
            foreach (var subject in toDeleteOnlySubjects)
            {
                var rowKey = GetDeleteRowKey(subject);
                batch.UpsertEntity(new TableEntity(partitionKey, rowKey), TableUpdateMode.Replace);
                if (batch.Count >= StorageUtility.MaxBatchSize)
                {
                    await batch.SubmitBatchAsync();
                    batch = new MutableTableTransactionalBatch(context.SubjectToOwnerTable);
                }
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        private static string GetDeletePartitionKey(string ownerType, string subjectType)
        {
            return $"{Separator}DELETE{Separator}{ownerType}{Separator}{subjectType}";
        }

        private static string GetDeleteRowKey(SubjectReference subject)
        {
            return $"{subject.PartitionKey}{Separator}{subject.RowKey}";
        }

        private async Task CommitOwnerToSubjectAsync(SetReferencesContext context)
        {
            // Persistence step 4: now that the subject-to-owner references have been updated, we can commit the change
            // by applying "ToAdd" and "ToDelete" sets to the "Committed" set and persisting the entity. This will only
            // succeed if the owner-to-subject entity has not be changed by another caller.
            var batch = new List<WideEntityOperation>();
            foreach ((var ownerRowKey, var cleanupContext) in context.OwnerRowKeyToInfo)
            {
                var newCommitted = context.OwnerRowKeyToSubjects[ownerRowKey];
                if (newCommitted.Count > 0)
                {
                    cleanupContext.Data.Committed = SortReferences(newCommitted);
                    cleanupContext.Data.ToAdd = Array.Empty<SubjectReference>();
                    cleanupContext.Data.ToDelete = Array.Empty<SubjectReference>();

                    var referencesBytes = Serialize(cleanupContext.Data);
                    batch.Add(WideEntityOperation.Replace(cleanupContext.Entity, referencesBytes));
                }
                else
                {
                    batch.Add(WideEntityOperation.Delete(cleanupContext.Entity));
                }
            }

            await _wideEntityService.ExecuteBatchAsync(context.OwnerToSubjectTableName, batch, allowBatchSplits: true);
        }

        private static byte[] Serialize<T>(T data)
        {
            return MessagePackSerializer.Serialize(data, NuGetInsightsMessagePack.Options);
        }

        private static T Deserialize<T>(WideEntity entity)
        {
            return MessagePackSerializer.Deserialize<T>(entity.GetStream(), NuGetInsightsMessagePack.Options);
        }

        private class SetReferencesContext
        {
            public SetReferencesContext(
                string ownerToSubjectTableName,
                TableClientWithRetryContext subjectToOwnerTable,
                string ownerType,
                string subjectType,
                string ownerPartitionKey,
                IReadOnlyDictionary<string, IReadOnlySet<SubjectEdge>> ownerRowKeyToSubjects)
            {
                OwnerToSubjectTableName = ownerToSubjectTableName;
                SubjectToOwnerTable = subjectToOwnerTable;
                OwnerType = ownerType;
                SubjectType = subjectType;
                OwnerPartitionKey = ownerPartitionKey;
                OwnerRowKeyToSubjects = ownerRowKeyToSubjects;
            }

            public string OwnerToSubjectTableName { get; }
            public TableClientWithRetryContext SubjectToOwnerTable { get; }
            public string OwnerType { get; }
            public string SubjectType { get; }
            public string OwnerPartitionKey { get; }
            public IReadOnlyDictionary<string, IReadOnlySet<SubjectEdge>> OwnerRowKeyToSubjects { get; }
            public List<EntityReference> ToAdd { get; } = new List<EntityReference>();
            public List<EntityReference> ToDelete { get; } = new List<EntityReference>();
            public Dictionary<string, OwnerToSubjectInfo> OwnerRowKeyToInfo { get; } = new Dictionary<string, OwnerToSubjectInfo>();

            public string OwnerToSubjectPartitionKey
            {
                get
                {
                    return GetOwnerToSubjectPartitionKey(OwnerType, OwnerPartitionKey, SubjectType);
                }
            }

            public string GetSubjectToOwnerPartitionKey(SubjectReference subject)
            {
                return ReferenceTracker.GetSubjectToOwnerPartitionKey(OwnerType, SubjectType, OwnerPartitionKey, subject);
            }
        }

        private static string GetOwnerToSubjectPartitionKey(string ownerType, string ownerPartitionKey, string subjectType)
        {
            return $"{ownerType}{Separator}{ownerPartitionKey}{Separator}{subjectType}";
        }

        private static string GetSubjectToOwnerPartitionKeyPrefix(string ownerType, string subjectType, SubjectReference subject)
        {
            return $"{subjectType}{Separator}{subject.PartitionKey}{Separator}{subject.RowKey}{Separator}{ownerType}{Separator}";
        }

        private static string GetSubjectToOwnerPartitionKey(string ownerType, string subjectType, string ownerPartitionKey, SubjectReference subject)
        {
            return $"{GetSubjectToOwnerPartitionKeyPrefix(ownerType, subjectType, subject)}{ownerPartitionKey}";
        }

        internal static void GuardNoSeparator(string paramName, string value)
        {
            if (value.Contains(Separator))
            {
                throw new ArgumentException($"The {paramName} must not contain a '{Separator}'.", paramName);
            }
        }

        private static string GetSubjectToOwnerRowKey(string ownerRowKey)
        {
            return ownerRowKey;
        }

        private class OwnerToSubjectInfo
        {
            public OwnerToSubjectInfo(OwnerToSubjectEdges data)
            {
                Data = data;
            }

            public OwnerToSubjectEdges Data { get; }
            public WideEntity Entity { get; set; }
        }

        [DebuggerDisplay("[{OwnerPartitionKey,nq}/{OwnerRowKey,nq}] has [{SubjectPartitionKey,nq}/{SubjectRowKey,nq}]")]
        private class EntityReference : IEquatable<EntityReference>
        {
            public EntityReference(string ownerPartitionKey, string ownerRowKey, string subjectPartitionKey, string subjectRowKey)
            {
                OwnerPartitionKey = ownerPartitionKey;
                OwnerRowKey = ownerRowKey;
                SubjectPartitionKey = subjectPartitionKey;
                SubjectRowKey = subjectRowKey;
            }

            public string OwnerPartitionKey { get; }
            public string OwnerRowKey { get; }
            public string SubjectPartitionKey { get; }
            public string SubjectRowKey { get; }

            public override bool Equals(object obj)
            {
                return Equals(obj as EntityReference);
            }

            public bool Equals(EntityReference other)
            {
                return other != null &&
                       OwnerPartitionKey == other.OwnerPartitionKey &&
                       OwnerRowKey == other.OwnerRowKey &&
                       SubjectPartitionKey == other.SubjectPartitionKey &&
                       SubjectRowKey == other.SubjectRowKey;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(OwnerPartitionKey, OwnerRowKey, SubjectPartitionKey, SubjectRowKey);
            }
        }
    }
}
