using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Logic
{
    public class ETagService : IETagService
    {
        public async Task<string> GetValueAsync(string name)
        {
            using (var entityContext = new EntityContext())
            {
                var etag = await GetETagAsync(entityContext, name);

                return etag?.Value;
            }
        }

        public async Task SetValueAsync(string name, string value)
        {
            using (var entityContext = new EntityContext())
            {
                var etag = await GetETagAsync(entityContext, name);
                if (etag == null)
                {
                    etag = new ETagEntity { Name = name };
                    entityContext.ETags.Add(etag);
                }

                etag.Value = value;

                await entityContext.SaveChangesAsync();
            }
        }

        public async Task ResetValueAsync(string name)
        {
            await SetValueAsync(name, value: null);
        }

        private async Task<ETagEntity> GetETagAsync(EntityContext entityContext, string name)
        {
            return await entityContext
                .ETags
                .FirstOrDefaultAsync(x => x.Name == name);
        }
    }
}
