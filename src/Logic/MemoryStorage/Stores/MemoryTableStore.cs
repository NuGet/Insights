// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

namespace NuGet.Insights.MemoryStorage
{
    public class MemoryTableStore
    {
        private const string ETagKey = "odata.etag";
        private const string TypeKeySuffix = "@odata.type";

        private readonly ConcurrentDictionary<(string, string), EntityData> _entityData = new();

        private readonly object _lock = new();

        private string _name;
        private bool _exists;

        public MemoryTableStore(string name)
        {
            _name = name;
        }

        public StorageResult<TableItem> GetTableItem()
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                return new(StorageResultType.Success, MakeTableItem());
            }
        }

        public StorageResult<TableItem> CreateIfNotExists()
        {
            lock (_lock)
            {
                if (_exists)
                {
                    return new(StorageResultType.AlreadyExists, MakeTableItem());
                }

                _exists = true;
                return new(StorageResultType.Success, MakeTableItem());
            }
        }

        public StorageResult<List<StorageResult<ETag?>>> SubmitTransaction(IReadOnlyList<TableTransactionAction> transactionActions)
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                if (transactionActions.Count < 1 || transactionActions.Count > StorageUtility.MaxBatchSize)
                {
                    throw new NotImplementedException();
                }

                var entityActions = new List<(EntityData?, TableTransactionAction)>();
                StorageResultType? failure = null;

                foreach (var action in transactionActions)
                {
                    StorageResultType entityResult;

                    if (_entityData.TryGetValue((action.Entity.PartitionKey, action.Entity.RowKey), out var existingEntity))
                    {
                        entityResult = (action.ActionType, action.ETag.ToString()) switch
                        {
                            // no etag not allowed
                            (TableTransactionActionType.Add, "") => StorageResultType.AlreadyExists,

                            (TableTransactionActionType.UpsertReplace, "") => StorageResultType.Success,

                            (TableTransactionActionType.UpsertMerge, "") => StorageResultType.Success,

                            // etag is required 
                            (TableTransactionActionType.Delete, "*") => StorageResultType.Success,
                            (TableTransactionActionType.Delete, _) when existingEntity.ETag == action.ETag => StorageResultType.Success,
                            (TableTransactionActionType.Delete, _) when existingEntity.ETag != default => StorageResultType.ETagMismatch,

                            (TableTransactionActionType.UpdateReplace, "*") => StorageResultType.Success,
                            (TableTransactionActionType.UpdateReplace, _) when existingEntity.ETag == action.ETag => StorageResultType.Success,
                            (TableTransactionActionType.UpdateReplace, _) when existingEntity.ETag != default => StorageResultType.ETagMismatch,

                            (TableTransactionActionType.UpdateMerge, "*") => StorageResultType.Success,
                            (TableTransactionActionType.UpdateMerge, _) when existingEntity.ETag == action.ETag => StorageResultType.Success,
                            (TableTransactionActionType.UpdateMerge, _) when existingEntity.ETag != default => StorageResultType.ETagMismatch,

                            (_, _) => throw new NotSupportedException($"Unexpected action type and etag combination: {action.ActionType} {action.ETag}"),
                        };
                    }
                    else
                    {
                        entityResult = (action.ActionType, action.ETag.ToString()) switch
                        {
                            // no etag not allowed
                            (TableTransactionActionType.Add, "") => StorageResultType.Success,

                            (TableTransactionActionType.UpsertReplace, "") => StorageResultType.Success,

                            (TableTransactionActionType.UpsertMerge, "") => StorageResultType.Success,

                            // etag is required 
                            (TableTransactionActionType.Delete, _) when action.ETag != default => StorageResultType.DoesNotExist,

                            (TableTransactionActionType.UpdateReplace, _) when action.ETag != default => StorageResultType.DoesNotExist,

                            (TableTransactionActionType.UpdateMerge, _) when action.ETag != default => StorageResultType.DoesNotExist,

                            (_, _) => throw new NotSupportedException($"Unexpected action type and etag combination: {action.ActionType} {action.ETag}"),
                        };
                    }

                    if (entityResult != StorageResultType.Success)
                    {
                        failure = entityResult;
                        break;
                    }
                    else
                    {
                        entityActions.Add((existingEntity, action));
                    }
                }

                var entityResults = new List<StorageResult<ETag?>>();
                if (failure.HasValue)
                {
                    foreach (var (existingEntity, action) in entityActions)
                    {
                        entityResults.Add(new(StorageResultType.Success, null));
                    }

                    entityResults.Add(new(failure.Value, null));
                }
                else
                {
                    foreach (var (existingEntity, action) in entityActions)
                    {
                        var entity = existingEntity;
                        if (entity is null)
                        {
                            entity = new EntityData();
                            if (!_entityData.TryAdd((action.Entity.PartitionKey, action.Entity.RowKey), entity))
                            {
                                throw new InvalidOperationException();
                            }
                        }

                        switch (action.ActionType)
                        {
                            case TableTransactionActionType.Add:
                                entity.UpdateReplace(action.Entity);
                                break;

                            case TableTransactionActionType.UpdateMerge:
                            case TableTransactionActionType.UpsertMerge:
                                entity.UpdateMerge(action.Entity);
                                break;

                            case TableTransactionActionType.UpdateReplace:
                            case TableTransactionActionType.UpsertReplace:
                                entity.UpdateReplace(action.Entity);
                                break;

                            case TableTransactionActionType.Delete:
                                if (!_entityData.TryRemove((action.Entity.PartitionKey, action.Entity.RowKey), out _))
                                {
                                    throw new InvalidOperationException();
                                }
                                entity = null;
                                break;

                            default:
                                throw new NotImplementedException();
                        }

                        entityResults.Add(new(StorageResultType.Success, entity?.ETag));
                    }
                }

                return new(StorageResultType.Success, entityResults);
            }
        }

        public StorageResult<List<T>> GetEntities<T>(
            Func<T, bool> filter,
            IReadOnlyList<string>? select) where T : ITableEntity
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                var entities = GetEntities(_entityData.Values, filter, select);
                return new(StorageResultType.Success, entities);
            }
        }

        public StorageResultType Delete()
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return StorageResultType.DoesNotExist;
                }

                _entityData.Clear();
                _exists = false;
                return StorageResultType.Success;
            }
        }

        public StorageResult<T> GetEntity<T>(
            string partitionKey,
            string rowKey,
            IReadOnlyList<string>? select) where T : ITableEntity
        {
            lock (_lock)
            {
                if (!_exists)
                {
                    return new(StorageResultType.DoesNotExist);
                }

                if (!_entityData.TryGetValue((partitionKey, rowKey), out var entityData))
                {
                    return new(StorageResultType.DoesNotExist);
                }

                var entity = GetEntities<T>([entityData], x => true, select).Single();
                return new(StorageResultType.Success, entity);
            }
        }

        private List<T> GetEntities<T>(
            IEnumerable<EntityData> entityData,
            Func<T, bool> filter,
            IReadOnlyList<string>? select) where T : ITableEntity
        {
            if (!_exists)
            {
                throw new InvalidOperationException();
            }

            if (select is null)
            {
                var entities = new List<T>();

                foreach (var entity in entityData)
                {
                    var tableEntity = EntityDataMapper<T>.ToTableEntity(entity.ToFields(), ignoreError: true);
                    if (tableEntity is not null && filter(tableEntity))
                    {
                        entities.Add(tableEntity);
                    }
                }

                entities.Sort((x, y) =>
                {
                    var c = string.CompareOrdinal(x.PartitionKey, y.PartitionKey);
                    if (c != 0)
                    {
                        return c;
                    }

                    return string.CompareOrdinal(x.RowKey, y.RowKey);
                });

                return entities;
            }
            else
            {
                var entitiesAndFields = new List<(T Entity, Dictionary<string, object> Fields)>();
                var fieldsToRemove = new HashSet<string>();
                foreach (var entity in entityData)
                {
                    var fields = entity.ToFields();
                    fieldsToRemove.UnionWith(fields.Keys);
                    var tableEntity = EntityDataMapper<T>.ToTableEntity(fields, ignoreError: true);
                    if (tableEntity is not null && filter(tableEntity))
                    {
                        entitiesAndFields.Add((tableEntity, fields));
                    }
                }

                entitiesAndFields.Sort((x, y) =>
                {
                    var c = string.CompareOrdinal(x.Entity.PartitionKey, y.Entity.PartitionKey);
                    if (c != 0)
                    {
                        return c;
                    }

                    return string.CompareOrdinal(x.Entity.RowKey, y.Entity.RowKey);
                });

                fieldsToRemove.ExceptWith(select);
                fieldsToRemove.Remove(ETagKey);

                var entities = new List<T>();
                foreach (var (_, fields) in entitiesAndFields)
                {
                    foreach (var fieldToRemove in fieldsToRemove)
                    {
                        fields.Remove(fieldToRemove);
                        fields.Remove($"{fieldToRemove}{TypeKeySuffix}");
                    }

                    entities.Add(EntityDataMapper<T>.ToTableEntity(fields, ignoreError: false)!);
                }

                return entities;
            }
        }

        private class EntityData
        {
            public static Func<ITableEntity, IDictionary<string, object>> GetOdataAnnotatedDictionary { get; }

            static EntityData()
            {
                var genericExtensionMethod = typeof(ITableEntity)
                    .Assembly
                    .GetType("Azure.Data.Tables.TableEntityExtensions")?
                    .GetMethod("ToOdataAnnotatedDictionary", BindingFlags.NonPublic | BindingFlags.Static);
                if (genericExtensionMethod is null)
                {
                    throw new NotImplementedException();
                }

                var extensionMethod = genericExtensionMethod.MakeGenericMethod(typeof(ITableEntity));
                GetOdataAnnotatedDictionary = x => (IDictionary<string, object>)extensionMethod.Invoke(null, [x])!;
            }

            private readonly Dictionary<string, object> _fields;

            public EntityData()
            {
                _fields = new();
            }

            public DateTimeOffset Timestamp { get; set; }
            public ETag ETag => Timestamp.ToMemoryETag(weak: true);

            public void UpdateReplace(ITableEntity entity)
            {
                Timestamp = DateTimeOffset.UtcNow;
                _fields.Clear();
                foreach (var field in GetNewFields(entity))
                {
                    _fields.Add(field.Key, field.Value);
                }
            }

            private static IEnumerable<KeyValuePair<string, object>> GetNewFields(ITableEntity entity)
            {
                return GetOdataAnnotatedDictionary(entity)
                    .Where(x => x.Value != null);
            }

            public void UpdateMerge(ITableEntity entity)
            {
                Timestamp = DateTimeOffset.UtcNow;
                foreach (var field in GetNewFields(entity))
                {
                    _fields[field.Key] = field.Value;
                }
            }

            public Dictionary<string, object> ToFields()
            {
                var fields = new Dictionary<string, object>(_fields.Count + 2) // timestamp and etag
                {
                    { "Timestamp", Timestamp.ToString("O") },
                    { ETagKey, ETag.ToString() }
                };

                foreach (var (key, value) in _fields)
                {
                    object serializedValue = value switch
                    {
                        Guid v => v.ToString(),
                        DateTimeOffset v => v.ToString("O"),
                        DateTime v => v.ToString("O"),
                        BinaryData v => Convert.ToBase64String(v.ToMemory().Span),
                        byte[] v => Convert.ToBase64String(v),
                        string v => v,
                        int v => v,
                        bool v => v,
                        double v => v,
                        _ => throw new NotSupportedException("Unsupported entity field type: " + value.GetType()),
                    };

                    fields.Add(key, serializedValue);
                }

                return fields;
            }
        }

        private static class EntityDataMapper<T>
        {
            private static Func<IDictionary<string, object>, T> ToTableEntityFunc { get; }

            static EntityDataMapper()
            {
                var genericExtensionMethod = typeof(ITableEntity)
                    .Assembly
                    .GetType("Azure.Data.Tables.DictionaryTableExtensions")?
                    .GetMethod("ToTableEntity", BindingFlags.NonPublic | BindingFlags.Static);
                if (genericExtensionMethod is null)
                {
                    throw new NotImplementedException();
                }

                var extensionMethod = genericExtensionMethod.MakeGenericMethod(typeof(T));
                ToTableEntityFunc = x => (T)extensionMethod.Invoke(null, [x, null])!;
            }

            public static T? ToTableEntity(IDictionary<string, object> fields, bool ignoreError)
            {
                try
                {
                    return ToTableEntityFunc(fields);
                }
                catch when (ignoreError)
                {
                    return default;
                }
            }
        }


        private TableItem MakeTableItem()
        {
            return new TableItem(_name);
        }
    }
}

