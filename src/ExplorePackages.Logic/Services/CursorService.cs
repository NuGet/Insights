using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Logic
{
    public class CursorService
    {
        /// <summary>
        /// This is the point in time that nuget.org started repository signing packages that had already been
        /// published and includes all packages that were repository signed at push time. In other words, we can start
        /// start cursors just before this time and still see all packages.
        /// </summary>
        public static readonly DateTimeOffset NuGetOrgMin = DateTimeOffset
            .Parse("2018-08-08T16:29:16.4488298Z")
            .Subtract(TimeSpan.FromTicks(1));

        private readonly EntityContextFactory _entityContextFactory;
        private readonly DateTimeOffset _defaultCursor;

        public CursorService(EntityContextFactory entityContextFactory) : this(
            entityContextFactory,
            NuGetOrgMin)
        {
        }

        public CursorService(
            EntityContextFactory entityContextFactory,
            DateTimeOffset defaultCursor)
        {
            _entityContextFactory = entityContextFactory;
            _defaultCursor = defaultCursor;
        }

        public async Task<CursorEntity> GetAsync(string name)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                return await GetCursorAsync(entityContext, name);
            }
        }

        public async Task<DateTimeOffset> GetValueAsync(string name)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var cursor = await GetCursorAsync(entityContext, name);

                return GetDateTimeOffset(cursor);
            }
        }

        public async Task<IReadOnlyList<string>> GetAllNamesAsync()
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                return await entityContext
                    .Cursors
                    .OrderBy(x => x.Name)
                    .Select(x => x.Name)
                    .ToListAsync();
            }
        }

        public async Task<DateTimeOffset> GetMinimumAsync(IReadOnlyList<string> names)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var distinctNames = names.Distinct().ToList();

                var cursors = await entityContext
                    .Cursors
                    .Where(x => distinctNames.Contains(x.Name))
                    .OrderBy(x => x.Value)
                    .ToListAsync();

                if (cursors.Count < distinctNames.Count)
                {
                    return _defaultCursor;
                }

                return GetDateTimeOffset(cursors.First());
            }
        }

        public async Task ResetValueAsync(string name)
        {
            await SetValueAsync(name, _defaultCursor);
        }

        public async Task SetValuesAsync(IReadOnlyList<string> names, DateTimeOffset value)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var existingCursors = await entityContext
                    .Cursors
                    .Where(x => names.Contains(x.Name))
                    .OrderBy(x => x.Value)
                    .ToListAsync();

                foreach (var existingCursor in existingCursors)
                {
                    existingCursor.Value = value.UtcTicks;
                }

                var newNames = names.Except(existingCursors.Select(x => x.Name));
                foreach (var newName in newNames)
                {
                    var newCursor = new CursorEntity
                    {
                        Name = newName,
                        Value = value.UtcTicks,
                    };

                    entityContext.Cursors.Add(newCursor);
                }

                await entityContext.SaveChangesAsync();
            }
        }

        public async Task SetValueAsync(string name, DateTimeOffset value)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var cursor = await GetCursorAsync(entityContext, name);
                if (cursor == null)
                {
                    cursor = new CursorEntity { Name = name };
                    entityContext.Cursors.Add(cursor);
                }

                cursor.Value = value.UtcTicks;

                await entityContext.SaveChangesAsync();
            }
        }

        public async Task EnsureExistsAsync(string name)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var cursor = await GetCursorAsync(entityContext, name);
                if (cursor == null)
                {
                    cursor = new CursorEntity
                    {
                        Name = name,
                        Value = _defaultCursor.Ticks,
                    };
                    entityContext.Cursors.Add(cursor);
                    await entityContext.SaveChangesAsync();
                }
            }
        }

        private async Task<CursorEntity> GetCursorAsync(IEntityContext entityContext, string name)
        {
            return await entityContext
                .Cursors
                .FirstOrDefaultAsync(x => x.Name == name);
        }

        private DateTimeOffset GetDateTimeOffset(CursorEntity cursor)
        {
            if (cursor == null)
            {
                return _defaultCursor;
            }

            return new DateTimeOffset(cursor.Value, TimeSpan.Zero);
        }
    }
}
