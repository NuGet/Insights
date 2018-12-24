using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Entities
{
    public class SqlServerEntityContext : BaseEntityContext<SqlServerEntityContext>
    {
        public SqlServerEntityContext(DbContextOptions<SqlServerEntityContext> options) : base(options)
        {
        }
    }
}
