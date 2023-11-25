// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CatalogScanCursorService
    {
        private delegate Task<DateTimeOffset> GetCursorValue(CatalogScanCursorService service);

        private const string FlatContainerCursorName = "NuGet.org flat container";

        private readonly CursorStorageService _cursorStorageService;
        private readonly IRemoteCursorClient _remoteCursorClient;
        private readonly ILogger<CatalogScanCursorService> _logger;

        public CatalogScanCursorService(
            CursorStorageService cursorStorageService,
            IRemoteCursorClient remoteCursorClient,
            ILogger<CatalogScanCursorService> logger)
        {
            _cursorStorageService = cursorStorageService;
            _remoteCursorClient = remoteCursorClient;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _cursorStorageService.InitializeAsync();
        }

        public static string GetCursorName(CatalogScanDriverType driverType)
        {
            return $"CatalogScan-{driverType}";
        }

        public async Task<CursorTableEntity> SetCursorAsync(CatalogScanDriverType driverType, DateTimeOffset value)
        {
            var entity = await _cursorStorageService.GetOrCreateAsync(GetCursorName(driverType), value);
            if (entity.Value != value)
            {
                entity.Value = value;
                await _cursorStorageService.UpdateAsync(entity);
            }

            return entity;
        }

        public async Task SetAllCursorsAsync(IEnumerable<CatalogScanDriverType> driverTypes, DateTimeOffset value)
        {
            var cursorNames = driverTypes.Select(GetCursorName).ToList();
            var cursors = await _cursorStorageService.GetOrCreateAllAsync(cursorNames, value);
            var cursorsToUpdate = cursors.Where(x => x.Value != value).ToList();
            foreach (var cursor in cursorsToUpdate)
            {
                cursor.Value = value;
            }

            await _cursorStorageService.UpdateAllAsync(cursorsToUpdate);
        }

        public async Task SetAllCursorsAsync(DateTimeOffset value)
        {
            await SetAllCursorsAsync(CatalogScanDriverMetadata.StartableDriverTypes, value);
        }

        public async Task<Dictionary<CatalogScanDriverType, CursorTableEntity>> GetCursorsAsync()
        {
            var nameToType = CatalogScanDriverMetadata.StartableDriverTypes.ToDictionary(GetCursorName);
            var cursors = await _cursorStorageService.GetOrCreateAllAsync(nameToType.Keys.ToList());
            return cursors.ToDictionary(x => nameToType[x.Name]);
        }

        public async Task<CursorTableEntity> GetCursorAsync(CatalogScanDriverType driverType)
        {
            return await _cursorStorageService.GetOrCreateAsync(GetCursorName(driverType));
        }

        public async Task<DateTimeOffset> GetCursorValueAsync(CatalogScanDriverType driverType)
        {
            var entity = await GetCursorAsync(driverType);
            return entity.Value;
        }

        public async Task<DateTimeOffset> GetSourceMaxAsync()
        {
            return await _remoteCursorClient.GetFlatContainerAsync();
        }

        public async Task<KeyValuePair<string, DateTimeOffset>> GetMinDependencyCursorValueAsync(CatalogScanDriverType driverType)
        {
            string name = null;
            var min = DateTimeOffset.MaxValue;

            var dependencies = CatalogScanDriverMetadata.GetDependencies(driverType);
            if (dependencies.Count == 0)
            {
                name = FlatContainerCursorName;
                min = await _remoteCursorClient.GetFlatContainerAsync();
            }
            else
            {
                foreach (var dependency in dependencies)
                {
                    var dependencyValue = await GetCursorValueAsync(dependency);
                    if (dependencyValue < min)
                    {
                        name = dependency.ToString();
                        min = dependencyValue;
                    }
                }
            }

            return KeyValuePair.Create(name, min);
        }
    }
}
