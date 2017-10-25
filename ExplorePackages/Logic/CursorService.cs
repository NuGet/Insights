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
        public async Task<DateTimeOffset> GetAsync(string name)
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
                    return DateTimeOffset.MinValue;
                }

                return GetDateTimeOffset(cursors.First());
            }
        }

        public async Task SetAsync(string name, DateTimeOffset value)
        {
            using (var entityContext = new EntityContext())
            {
                var cursor = await GetCursorAsync(entityContext, name);
                if (cursor == null)
                {
                    cursor = new Cursor { Name = name };
                    entityContext.Cursors.Add(cursor);
                }

                cursor.SetDateTimeOffset(value);

                await entityContext.SaveChangesAsync();
            }
        }

        public async Task EnsureExistsAsync(string name)
        {
            using (var entityContext = new EntityContext())
            {
                var cursor = await GetCursorAsync(entityContext, name);
                if (cursor == null)
                {
                    cursor = new Cursor
                    {
                        Name = name,
                        Value = DateTimeOffset.MinValue.Ticks,
                    };
                    entityContext.Cursors.Add(cursor);
                    await entityContext.SaveChangesAsync();
                }
            }
        }

        private async Task<Cursor> GetCursorAsync(EntityContext entityContext, string name)
        {
            return await entityContext
                .Cursors
                .FirstOrDefaultAsync(x => x.Name == name);
        }

        private static DateTimeOffset GetDateTimeOffset(Cursor cursor)
        {
            if (cursor == null)
            {
                return DateTimeOffset.MinValue;
            }

            return cursor.GetDateTimeOffset();
        }
    }
}
