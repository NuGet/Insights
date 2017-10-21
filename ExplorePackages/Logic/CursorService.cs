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
        private readonly EntityContext _entityContext;

        public CursorService(EntityContext entityContext)
        {
            _entityContext = entityContext;
        }

        public async Task<DateTimeOffset> GetAsync(string name)
        {
            var cursor = await GetCursorAsync(name);

            return GetDateTimeOffset(cursor);
        }

        public async Task<DateTimeOffset> GetMinimumAsync(IReadOnlyList<string> names)
        {
            var cursors = await _entityContext
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

        public async Task SetAsync(string name, DateTimeOffset value)
        {
            var cursor = await GetCursorAsync(name);
            if (cursor == null)
            {
                cursor = new Cursor { Name = name };
                _entityContext.Cursors.Add(cursor);
            }

            cursor.SetDateTimeOffset(value);

            await _entityContext.SaveChangesAsync();
        }

        private async Task<Cursor> GetCursorAsync(string name)
        {
            return await _entityContext
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
