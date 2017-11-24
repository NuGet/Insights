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
        private static readonly DateTimeOffset DefaultCursor = DateTimeOffset.MinValue;

        public async Task<CursorEntity> GetAsync(string name)
        {
            using (var entityContext = new EntityContext())
            {
                return await GetCursorAsync(entityContext, name);
            }
        }

        public async Task<DateTimeOffset> GetValueAsync(string name)
        {
            using (var entityContext = new EntityContext())
            {
                var cursor = await GetCursorAsync(entityContext, name);

                return GetDateTimeOffset(cursor);
            }
        }

        public async Task<DateTimeOffset> GetMinimumAsync(IReadOnlyList<string> names)
        {
            using (var entityContext = new EntityContext())
            {
                var cursors = await entityContext
                       .Cursors
                       .Where(x => names.Contains(x.Name))
                       .OrderBy(x => x.Value)
                       .ToListAsync();

                if (cursors.Count < names.Count)
                {
                    return DefaultCursor;
                }

                return GetDateTimeOffset(cursors.First());
            }
        }

        public async Task SetValueAsync(string name, long value)
        {
            using (var entityContext = new EntityContext())
            {
                var cursor = await GetCursorAsync(entityContext, name);
                if (cursor == null)
                {
                    cursor = new CursorEntity { Name = name };
                    entityContext.Cursors.Add(cursor);
                }

                cursor.Value = value;

                await entityContext.SaveChangesAsync();
            }
        }

        public async Task ResetValueAsync(string name)
        {
            await SetValueAsync(name, DefaultCursor);
        }

        public async Task SetValueAsync(string name, DateTimeOffset value)
        {
            await SetValueAsync(name, value.UtcTicks);
        }

        public async Task EnsureExistsAsync(string name)
        {
            using (var entityContext = new EntityContext())
            {
                var cursor = await GetCursorAsync(entityContext, name);
                if (cursor == null)
                {
                    cursor = new CursorEntity
                    {
                        Name = name,
                        Value = DefaultCursor.Ticks,
                    };
                    entityContext.Cursors.Add(cursor);
                    await entityContext.SaveChangesAsync();
                }
            }
        }

        private async Task<CursorEntity> GetCursorAsync(EntityContext entityContext, string name)
        {
            return await entityContext
                .Cursors
                .FirstOrDefaultAsync(x => x.Name == name);
        }

        private static DateTimeOffset GetDateTimeOffset(CursorEntity cursor)
        {
            if (cursor == null)
            {
                return DefaultCursor;
            }

            return new DateTimeOffset(cursor.Value, TimeSpan.Zero);
        }
    }
}
